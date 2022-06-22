// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
#if FEATURE_PIPE_SECURITY
using System.Security.Principal;
#endif

#if FEATURE_APM
using Microsoft.Build.Eventing;
#endif
using Microsoft.Build.Exceptions;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Utilities;

using BackendNativeMethods = Microsoft.Build.BackEnd.NativeMethods;
using Task = System.Threading.Tasks.Task;
using Microsoft.Build.Framework;
using Microsoft.Build.BackEnd.Logging;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Contains the shared pieces of code from NodeProviderOutOfProc
    /// and NodeProviderOutOfProcTaskHost.
    /// </summary>
    internal abstract class NodeProviderOutOfProcBase
    {
        /// <summary>
        /// The maximum number of bytes to write
        /// </summary>
        private const int MaxPacketWriteSize = 1048576;

        /// <summary>
        /// The number of times to retry creating an out-of-proc node.
        /// </summary>
        private const int NodeCreationRetries = 10;

        /// <summary>
        /// The amount of time to wait for an out-of-proc node to spool up before we give up.
        /// </summary>
        private const int TimeoutForNewNodeCreation = 30000;

        /// <summary>
        /// The amount of time to wait for an out-of-proc node to exit.
        /// </summary>
        private const int TimeoutForWaitForExit = 30000;

        /// <summary>
        /// The build component host.
        /// </summary>
        private IBuildComponentHost _componentHost;

        /// <summary>
        /// Keeps track of the processes we've already checked for nodes so we don't check them again.
        /// </summary>
        private HashSet<string> _processesToIgnore = new HashSet<string>();

        /// <summary>
        /// Delegate used to tell the node provider that a context has terminated.
        /// </summary>
        /// <param name="nodeId">The id of the node which terminated.</param>
        internal delegate void NodeContextTerminateDelegate(int nodeId);

        /// <summary>
        /// The build component host.
        /// </summary>
        protected IBuildComponentHost ComponentHost
        {
            get { return _componentHost; }
            set { _componentHost = value; }
        }

        /// <summary>
        /// Sends data to the specified node.
        /// </summary>
        /// <param name="context">The node to which data shall be sent.</param>
        /// <param name="packet">The packet to send.</param>
        protected void SendData(NodeContext context, INodePacket packet)
        {
            ErrorUtilities.VerifyThrowArgumentNull(packet, nameof(packet));
            context.SendData(packet);
        }

        /// <summary>
        /// Shuts down all of the connected managed nodes.
        /// </summary>
        /// <param name="contextsToShutDown">List of the contexts to be shut down</param>
        /// <param name="enableReuse">Flag indicating if nodes should prepare for reuse.</param>
        protected void ShutdownConnectedNodes(List<NodeContext> contextsToShutDown, bool enableReuse)
        {
            // Send the build completion message to the nodes, causing them to shutdown or reset.
            _processesToIgnore.Clear();

            // We wait for child nodes to exit to avoid them changing the terminal
            // after this process terminates.
            bool waitForExit =  !enableReuse &&
                                !Console.IsInputRedirected &&
                                Traits.Instance.EscapeHatches.EnsureStdOutForChildNodesIsPrimaryStdout;

            Task[] waitForExitTasks = waitForExit && contextsToShutDown.Count > 0 ? new Task[contextsToShutDown.Count] : null;
            int i = 0;
            var loggingService = _componentHost.LoggingService;
            foreach (NodeContext nodeContext in contextsToShutDown)
            {
                if (nodeContext is null)
                {
                    continue;
                }
                nodeContext.SendData(new NodeBuildComplete(enableReuse));
                if (waitForExit)
                {
                    waitForExitTasks[i++] = nodeContext.WaitForExitAsync(loggingService);
                }
            }
            if (waitForExitTasks != null)
            {
                Task.WaitAll(waitForExitTasks);
            }
        }

        /// <summary>
        /// Shuts down all of the managed nodes permanently.
        /// </summary>
        /// <param name="nodeReuse">Whether to reuse the node</param>
        /// <param name="terminateNode">Delegate used to tell the node provider that a context has terminated</param>
        protected void ShutdownAllNodes(bool nodeReuse, NodeContextTerminateDelegate terminateNode)
        {
            // INodePacketFactory
            INodePacketFactory factory = new NodePacketFactory();

            List<Process> nodeProcesses = GetPossibleRunningNodes().nodeProcesses;

            // Find proper MSBuildTaskHost executable name
            string msbuildtaskhostExeName = NodeProviderOutOfProcTaskHost.TaskHostNameForClr2TaskHost;

            // Search for all instances of msbuildtaskhost process and add them to the process list
            nodeProcesses.AddRange(new List<Process>(Process.GetProcessesByName(Path.GetFileNameWithoutExtension(msbuildtaskhostExeName))));

            // For all processes in the list, send signal to terminate if able to connect
            foreach (Process nodeProcess in nodeProcesses)
            {
                // A 2013 comment suggested some nodes take this long to respond, so a smaller timeout would miss nodes.
                int timeout = 30;

                // Attempt to connect to the process with the handshake without low priority.
                Stream nodeStream = TryConnectToProcess(nodeProcess.Id, timeout, NodeProviderOutOfProc.GetHandshake(nodeReuse, false));

                if (nodeStream == null)
                {
                    // If we couldn't connect attempt to connect to the process with the handshake including low priority.
                    nodeStream = TryConnectToProcess(nodeProcess.Id, timeout, NodeProviderOutOfProc.GetHandshake(nodeReuse, true));
                }

                if (nodeStream != null)
                {
                    // If we're able to connect to such a process, send a packet requesting its termination
                    CommunicationsUtilities.Trace("Shutting down node with pid = {0}", nodeProcess.Id);
                    NodeContext nodeContext = new NodeContext(0, nodeProcess, nodeStream, factory, terminateNode);
                    nodeContext.SendData(new NodeBuildComplete(false /* no node reuse */));
                    nodeStream.Dispose();
                }
            }
        }

        /// <summary>
        /// Finds or creates a child process which can act as a node.
        /// </summary>
        /// <returns>The pipe stream representing the node.</returns>
        protected NodeContext GetNode(string msbuildLocation, string commandLineArgs, int nodeId, INodePacketFactory factory, Handshake hostHandshake, NodeContextTerminateDelegate terminateNode)
        {
#if DEBUG
            if (Execution.BuildManager.WaitForDebugger)
            {
                commandLineArgs += " /wfd";
            }
#endif

            if (String.IsNullOrEmpty(msbuildLocation))
            {
                msbuildLocation = _componentHost.BuildParameters.NodeExeLocation;
            }

            if (String.IsNullOrEmpty(msbuildLocation))
            {
                string msbuildExeName = Environment.GetEnvironmentVariable("MSBUILD_EXE_NAME");

                if (!String.IsNullOrEmpty(msbuildExeName))
                {
                    // we assume that MSBUILD_EXE_NAME is, in fact, just the name.
                    msbuildLocation = Path.Combine(msbuildExeName, ".exe");
                }
            }

#if FEATURE_NODE_REUSE
            // Try to connect to idle nodes if node reuse is enabled.
            if (_componentHost.BuildParameters.EnableNodeReuse)
            {
                (string expectedProcessName, List<Process> processes) runningNodesTuple = GetPossibleRunningNodes(msbuildLocation);

                CommunicationsUtilities.Trace("Attempting to connect to each existing {1} process in turn to establish node {0}...", nodeId, runningNodesTuple.expectedProcessName);
                foreach (Process nodeProcess in runningNodesTuple.processes)
                {
                    if (nodeProcess.Id == Process.GetCurrentProcess().Id)
                    {
                        continue;
                    }

                    // Get the full context of this inspection so that we can always skip this process when we have the same taskhost context
                    string nodeLookupKey = GetProcessesToIgnoreKey(hostHandshake, nodeProcess.Id);
                    if (_processesToIgnore.Contains(nodeLookupKey))
                    {
                        continue;
                    }

                    // We don't need to check this again
                    _processesToIgnore.Add(nodeLookupKey);

                    // Attempt to connect to each process in turn.
                    Stream nodeStream = TryConnectToProcess(nodeProcess.Id, 0 /* poll, don't wait for connections */, hostHandshake);
                    if (nodeStream != null)
                    {
                        // Connection successful, use this node.
                        CommunicationsUtilities.Trace("Successfully connected to existed node {0} which is PID {1}", nodeId, nodeProcess.Id);
                        return new NodeContext(nodeId, nodeProcess, nodeStream, factory, terminateNode);
                    }
                }
            }
#endif

            // None of the processes we tried to connect to allowed a connection, so create a new one.
            // We try this in a loop because it is possible that there is another MSBuild multiproc
            // host process running somewhere which is also trying to create nodes right now.  It might
            // find our newly created node and connect to it before we get a chance.
            CommunicationsUtilities.Trace("Could not connect to existing process, now creating a process...");
            int retries = NodeCreationRetries;
            while (retries-- > 0)
            {
#if FEATURE_NET35_TASKHOST
                // We will also check to see if .NET 3.5 is installed in the case where we need to launch a CLR2 OOP TaskHost.
                // Failure to detect this has been known to stall builds when Windows pops up a related dialog.
                // It's also a waste of time when we attempt several times to launch multiple MSBuildTaskHost.exe (CLR2 TaskHost)
                // nodes because we should never be able to connect in this case.
                string taskHostNameForClr2TaskHost = Path.GetFileNameWithoutExtension(NodeProviderOutOfProcTaskHost.TaskHostNameForClr2TaskHost);
                if (Path.GetFileNameWithoutExtension(msbuildLocation).Equals(taskHostNameForClr2TaskHost, StringComparison.OrdinalIgnoreCase))
                {
                    if (FrameworkLocationHelper.GetPathToDotNetFrameworkV35(DotNetFrameworkArchitecture.Current) == null)
                    {
                        CommunicationsUtilities.Trace
                            (
                                "Failed to launch node from {0}. The required .NET Framework v3.5 is not installed or enabled. CommandLine: {1}",
                                msbuildLocation,
                                commandLineArgs
                            );

                        string nodeFailedToLaunchError = ResourceUtilities.GetResourceString("TaskHostNodeFailedToLaunchErrorCodeNet35NotInstalled");
                        throw new NodeFailedToLaunchException(null, nodeFailedToLaunchError);
                    }
                }
#endif

                // Create the node process
                Process msbuildProcess = LaunchNode(msbuildLocation, commandLineArgs);
                _processesToIgnore.Add(GetProcessesToIgnoreKey(hostHandshake, msbuildProcess.Id));

                // Note, when running under IMAGEFILEEXECUTIONOPTIONS registry key to debug, the process ID
                // gotten back from CreateProcess is that of the debugger, which causes this to try to connect
                // to the debugger process. Instead, use MSBUILDDEBUGONSTART=1

                // Now try to connect to it.
                Stream nodeStream = TryConnectToProcess(msbuildProcess.Id, TimeoutForNewNodeCreation, hostHandshake);
                if (nodeStream != null)
                {
                    // Connection successful, use this node.
                    CommunicationsUtilities.Trace("Successfully connected to created node {0} which is PID {1}", nodeId, msbuildProcess.Id);
                    return new NodeContext(nodeId, msbuildProcess, nodeStream, factory, terminateNode);
                }
            }

            // We were unable to launch a node.
            CommunicationsUtilities.Trace("FAILED TO CONNECT TO A CHILD NODE");
            return null;
        }

        /// <summary>
        /// Finds processes named after either msbuild or msbuildtaskhost.
        /// </summary>
        /// <param name="msbuildLocation"></param>
        /// <returns>
        /// Item 1 is the name of the process being searched for.
        /// Item 2 is the list of processes themselves.
        /// </returns>
        private (string expectedProcessName, List<Process> nodeProcesses) GetPossibleRunningNodes(string msbuildLocation = null)
        {
            if (String.IsNullOrEmpty(msbuildLocation))
            {
                msbuildLocation = "MSBuild.exe";
            }

            var expectedProcessName = Path.GetFileNameWithoutExtension(GetCurrentHost() ?? msbuildLocation);

            List<Process> nodeProcesses = new List<Process>(Process.GetProcessesByName(expectedProcessName));

            // Trivial sort to try to prefer most recently used nodes
            nodeProcesses.Sort((left, right) => left.Id - right.Id);

            return (expectedProcessName, nodeProcesses);
        }

        /// <summary>
        /// Generate a string from task host context and the remote process to be used as key to lookup processes we have already
        /// attempted to connect to or are already connected to
        /// </summary>
        private string GetProcessesToIgnoreKey(Handshake hostHandshake, int nodeProcessId)
        {
            return hostHandshake.ToString() + "|" + nodeProcessId.ToString(CultureInfo.InvariantCulture);
        }

#if !FEATURE_PIPEOPTIONS_CURRENTUSERONLY
        //  This code needs to be in a separate method so that we don't try (and fail) to load the Windows-only APIs when JIT-ing the code
        //  on non-Windows operating systems
        private void ValidateRemotePipeSecurityOnWindows(NamedPipeClientStream nodeStream)
        {
            SecurityIdentifier identifier = WindowsIdentity.GetCurrent().Owner;
#if FEATURE_PIPE_SECURITY
            PipeSecurity remoteSecurity = nodeStream.GetAccessControl();
#else
            var remoteSecurity = new PipeSecurity(nodeStream.SafePipeHandle, System.Security.AccessControl.AccessControlSections.Access |
                System.Security.AccessControl.AccessControlSections.Owner | System.Security.AccessControl.AccessControlSections.Group);
#endif
            IdentityReference remoteOwner = remoteSecurity.GetOwner(typeof(SecurityIdentifier));
            if (remoteOwner != identifier)
            {
                CommunicationsUtilities.Trace("The remote pipe owner {0} does not match {1}", remoteOwner.Value, identifier.Value);
                throw new UnauthorizedAccessException();
            }
        }
#endif

        /// <summary>
        /// Attempts to connect to the specified process.
        /// </summary>
        private Stream TryConnectToProcess(int nodeProcessId, int timeout, Handshake handshake)
        {
            // Try and connect to the process.
            string pipeName = NamedPipeUtil.GetPipeNameOrPath("MSBuild" + nodeProcessId);

            NamedPipeClientStream nodeStream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous
#if FEATURE_PIPEOPTIONS_CURRENTUSERONLY
                                                                         | PipeOptions.CurrentUserOnly
#endif
                                                                         );
            CommunicationsUtilities.Trace("Attempting connect to PID {0} with pipe {1} with timeout {2} ms", nodeProcessId, pipeName, timeout);

            try
            {
                nodeStream.Connect(timeout);

#if !FEATURE_PIPEOPTIONS_CURRENTUSERONLY
                if (NativeMethodsShared.IsWindows && !NativeMethodsShared.IsMono)
                {
                    // Verify that the owner of the pipe is us.  This prevents a security hole where a remote node has
                    // been faked up with ACLs that would let us attach to it.  It could then issue fake build requests back to
                    // us, potentially causing us to execute builds that do harmful or unexpected things.  The pipe owner can
                    // only be set to the user's own SID by a normal, unprivileged process.  The conditions where a faked up
                    // remote node could set the owner to something else would also let it change owners on other objects, so
                    // this would be a security flaw upstream of us.
                    ValidateRemotePipeSecurityOnWindows(nodeStream);
                }
#endif

                int[] handshakeComponents = handshake.RetrieveHandshakeComponents();
                for (int i = 0; i < handshakeComponents.Length; i++)
                {
                    CommunicationsUtilities.Trace("Writing handshake part {0} ({1}) to pipe {2}", i, handshakeComponents[i], pipeName);
                    nodeStream.WriteIntForHandshake(handshakeComponents[i]);
                }

                // This indicates that we have finished all the parts of our handshake; hopefully the endpoint has as well.
                nodeStream.WriteEndOfHandshakeSignal();

                CommunicationsUtilities.Trace("Reading handshake from pipe {0}", pipeName);

#if NETCOREAPP2_1_OR_GREATER || MONO
                nodeStream.ReadEndOfHandshakeSignal(true, timeout);
#else
                nodeStream.ReadEndOfHandshakeSignal(true);
#endif
                // We got a connection.
                CommunicationsUtilities.Trace("Successfully connected to pipe {0}...!", pipeName);
                return nodeStream;
            }
            catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
            {
                // Can be:
                // UnauthorizedAccessException -- Couldn't connect, might not be a node.
                // IOException -- Couldn't connect, already in use.
                // TimeoutException -- Couldn't connect, might not be a node.
                // InvalidOperationException – Couldn’t connect, probably a different build
                CommunicationsUtilities.Trace("Failed to connect to pipe {0}. {1}", pipeName, e.Message.TrimEnd());

                // If we don't close any stream, we might hang up the child
                nodeStream?.Dispose();
            }

            return null;
        }

        /// <summary>
        /// Creates a new MSBuild process
        /// </summary>
        private Process LaunchNode(string msbuildLocation, string commandLineArgs)
        {
            // Should always have been set already.
            ErrorUtilities.VerifyThrowInternalLength(msbuildLocation, nameof(msbuildLocation));

            if (!FileSystems.Default.FileExists(msbuildLocation))
            {
                throw new BuildAbortedException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("CouldNotFindMSBuildExe", msbuildLocation));
            }

            // Repeat the executable name as the first token of the command line because the command line
            // parser logic expects it and will otherwise skip the first argument
            commandLineArgs = $"\"{msbuildLocation}\" {commandLineArgs}";

            BackendNativeMethods.STARTUP_INFO startInfo = new();
            startInfo.cb = Marshal.SizeOf<BackendNativeMethods.STARTUP_INFO>();

            // Null out the process handles so that the parent process does not wait for the child process
            // to exit before it can exit.
            uint creationFlags = 0;
            if (Traits.Instance.EscapeHatches.EnsureStdOutForChildNodesIsPrimaryStdout)
            {
                creationFlags = BackendNativeMethods.NORMALPRIORITYCLASS;
            }

            if (String.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSBUILDNODEWINDOW")))
            {
                if (!Traits.Instance.EscapeHatches.EnsureStdOutForChildNodesIsPrimaryStdout)
                {
                    // Redirect the streams of worker nodes so that this MSBuild.exe's
                    // parent doesn't wait on idle worker nodes to close streams
                    // after the build is complete.
                    startInfo.hStdError = BackendNativeMethods.InvalidHandle;
                    startInfo.hStdInput = BackendNativeMethods.InvalidHandle;
                    startInfo.hStdOutput = BackendNativeMethods.InvalidHandle;
                    startInfo.dwFlags = BackendNativeMethods.STARTFUSESTDHANDLES;
                    creationFlags |= BackendNativeMethods.CREATENOWINDOW;
                }
            }
            else
            {
                creationFlags |= BackendNativeMethods.CREATE_NEW_CONSOLE;
            }

            CommunicationsUtilities.Trace("Launching node from {0}", msbuildLocation);

            string exeName = msbuildLocation;

#if RUNTIME_TYPE_NETCORE || MONO
            // Mono automagically uses the current mono, to execute a managed assembly
            if (!NativeMethodsShared.IsMono)
            {
                // Run the child process with the same host as the currently-running process.
                exeName = GetCurrentHost();
            }
#endif

            if (!NativeMethodsShared.IsWindows)
            {
                ProcessStartInfo processStartInfo = new ProcessStartInfo();
                processStartInfo.FileName = exeName;
                processStartInfo.Arguments = commandLineArgs;
                if (!Traits.Instance.EscapeHatches.EnsureStdOutForChildNodesIsPrimaryStdout)
                {
                    // Redirect the streams of worker nodes so that this MSBuild.exe's
                    // parent doesn't wait on idle worker nodes to close streams
                    // after the build is complete.
                    processStartInfo.RedirectStandardInput = true;
                    processStartInfo.RedirectStandardOutput = true;
                    processStartInfo.RedirectStandardError = true;
                    processStartInfo.CreateNoWindow = (creationFlags | BackendNativeMethods.CREATENOWINDOW) == BackendNativeMethods.CREATENOWINDOW;
                }
                processStartInfo.UseShellExecute = false;

                Process process;
                try
                {
                    process = Process.Start(processStartInfo);
                }
                catch (Exception ex)
                {
                    CommunicationsUtilities.Trace
                       (
                           "Failed to launch node from {0}. CommandLine: {1}" + Environment.NewLine + "{2}",
                           msbuildLocation,
                           commandLineArgs,
                           ex.ToString()
                       );

                    throw new NodeFailedToLaunchException(ex);
                }

                CommunicationsUtilities.Trace("Successfully launched {1} node with PID {0}", process.Id, exeName);
                return process;
            }
            else
            {
#if RUNTIME_TYPE_NETCORE
                // Repeat the executable name in the args to suit CreateProcess
                commandLineArgs = $"\"{exeName}\" {commandLineArgs}";
#endif

                BackendNativeMethods.PROCESS_INFORMATION processInfo = new();
                BackendNativeMethods.SECURITY_ATTRIBUTES processSecurityAttributes = new();
                BackendNativeMethods.SECURITY_ATTRIBUTES threadSecurityAttributes = new();
                processSecurityAttributes.nLength = Marshal.SizeOf<BackendNativeMethods.SECURITY_ATTRIBUTES>();
                threadSecurityAttributes.nLength = Marshal.SizeOf<BackendNativeMethods.SECURITY_ATTRIBUTES>();

                bool result = BackendNativeMethods.CreateProcess
                    (
                        exeName,
                        commandLineArgs,
                        ref processSecurityAttributes,
                        ref threadSecurityAttributes,
                        false,
                        creationFlags,
                        BackendNativeMethods.NullPtr,
                        null,
                        ref startInfo,
                        out processInfo
                    );

                if (!result)
                {
                    // Creating an instance of this exception calls GetLastWin32Error and also converts it to a user-friendly string.
                    System.ComponentModel.Win32Exception e = new System.ComponentModel.Win32Exception();

                    CommunicationsUtilities.Trace
                        (
                            "Failed to launch node from {0}. System32 Error code {1}. Description {2}. CommandLine: {2}",
                            msbuildLocation,
                            e.NativeErrorCode.ToString(CultureInfo.InvariantCulture),
                            e.Message,
                            commandLineArgs
                        );

                    throw new NodeFailedToLaunchException(e.NativeErrorCode.ToString(CultureInfo.InvariantCulture), e.Message);
                }

                int childProcessId = processInfo.dwProcessId;

                if (processInfo.hProcess != IntPtr.Zero && processInfo.hProcess != NativeMethods.InvalidHandle)
                {
                    NativeMethodsShared.CloseHandle(processInfo.hProcess);
                }

                if (processInfo.hThread != IntPtr.Zero && processInfo.hThread != NativeMethods.InvalidHandle)
                {
                    NativeMethodsShared.CloseHandle(processInfo.hThread);
                }

                CommunicationsUtilities.Trace("Successfully launched {1} node with PID {0}", childProcessId, exeName);
                return Process.GetProcessById(childProcessId);
            }
        }

#if RUNTIME_TYPE_NETCORE || MONO
        private static string CurrentHost;
#endif

        /// <summary>
        /// Identify the .NET host of the current process.
        /// </summary>
        /// <returns>The full path to the executable hosting the current process, or null if running on Full Framework on Windows.</returns>
        private static string GetCurrentHost()
        {
#if RUNTIME_TYPE_NETCORE || MONO
            if (CurrentHost == null)
            {
                string dotnetExe = Path.Combine(FileUtilities.GetFolderAbove(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, 2),
                    NativeMethodsShared.IsWindows ? "dotnet.exe" : "dotnet");
                if (File.Exists(dotnetExe))
                {
                    CurrentHost = dotnetExe;
                }
                else
                {
                    using (Process currentProcess = Process.GetCurrentProcess())
                    {
                        CurrentHost = currentProcess.MainModule.FileName;
                    }
                }
            }

            return CurrentHost;
#else
            return null;
#endif
        }

        /// <summary>
        /// Class which wraps up the communications infrastructure for a given node.
        /// </summary>
        internal class NodeContext
        {
            enum ExitPacketState
            {
                None,
                ExitPacketQueued,
                ExitPacketSent
            }

            // The pipe(s) used to communicate with the node.
            private Stream _clientToServerStream;
            private Stream _serverToClientStream;

            /// <summary>
            /// The factory used to create packets from data read off the pipe.
            /// </summary>
            private INodePacketFactory _packetFactory;

            /// <summary>
            /// The node id assigned by the node provider.
            /// </summary>
            private int _nodeId;

            /// <summary>
            /// The node process.
            /// </summary>
            private readonly Process _process;

            /// <summary>
            /// An array used to store the header byte for each packet when read.
            /// </summary>
            private byte[] _headerByte;

            /// <summary>
            /// A buffer typically big enough to handle a packet body.
            /// We use this as a convenient way to manage and cache a byte[] that's resized
            /// automatically to fit our payload.
            /// </summary>
            private MemoryStream _readBufferMemoryStream;

            /// <summary>
            /// A reusable buffer for writing packets.
            /// </summary>
            private MemoryStream _writeBufferMemoryStream;

            /// <summary>
            /// A queue used for enqueuing packets to write to the stream asynchronously.
            /// </summary>
            private BlockingCollection<INodePacket> _packetWriteQueue = new BlockingCollection<INodePacket>();

            /// <summary>
            /// A task representing the last packet write, so we can chain packet writes one after another.
            /// We want to queue up writing packets on a separate thread asynchronously, but serially.
            /// Each task drains the <see cref="_packetWriteQueue"/>
            /// </summary>
            private Task _packetWriteDrainTask = Task.CompletedTask;

            /// <summary>
            /// Delegate called when the context terminates.
            /// </summary>
            private NodeContextTerminateDelegate _terminateDelegate;

            /// <summary>
            /// Tracks the state of the packet sent to terminate the node.
            /// </summary>
            private ExitPacketState _exitPacketState;

            /// <summary>
            /// Per node read buffers
            /// </summary>
            private SharedReadBuffer _sharedReadBuffer;

            /// <summary>
            /// Constructor.
            /// </summary>
            public NodeContext(int nodeId, Process process,
                Stream nodePipe,
                INodePacketFactory factory, NodeContextTerminateDelegate terminateDelegate)
            {
                _nodeId = nodeId;
                _process = process;
                _clientToServerStream = nodePipe;
                _serverToClientStream = nodePipe;
                _packetFactory = factory;
                _headerByte = new byte[5]; // 1 for the packet type, 4 for the body length
                _readBufferMemoryStream = new MemoryStream();
                _writeBufferMemoryStream = new MemoryStream();
                _terminateDelegate = terminateDelegate;
                _sharedReadBuffer = InterningBinaryReader.CreateSharedBuffer();
            }

            /// <summary>
            /// Starts a new asynchronous read operation for this node.
            /// </summary>
            public void BeginAsyncPacketRead()
            {
#if FEATURE_APM
                _clientToServerStream.BeginRead(_headerByte, 0, _headerByte.Length, HeaderReadComplete, this);
#else
                ThreadPool.QueueUserWorkItem(delegate
                {
                    var ignored = RunPacketReadLoopAsync();
                });
#endif
            }

#if !FEATURE_APM
            public async Task RunPacketReadLoopAsync()
            {
                while (true)
                {
                    try
                    {
                        int bytesRead = await CommunicationsUtilities.ReadAsync(_clientToServerStream, _headerByte, _headerByte.Length);
                        if (!ProcessHeaderBytesRead(bytesRead))
                        {
                            return;
                        }
                    }
                    catch (IOException e)
                    {
                        CommunicationsUtilities.Trace(_nodeId, "EXCEPTION in RunPacketReadLoopAsync: {0}", e);
                        _packetFactory.RoutePacket(_nodeId, new NodeShutdown(NodeShutdownReason.ConnectionFailed));
                        Close();
                        return;
                    }

                    NodePacketType packetType = (NodePacketType)_headerByte[0];
                    int packetLength = BinaryPrimitives.ReadInt32LittleEndian(new Span<byte>(_headerByte, 1, 4));

                    _readBufferMemoryStream.SetLength(packetLength);
                    byte[] packetData = _readBufferMemoryStream.GetBuffer();

                    try
                    {
                        int bytesRead = await CommunicationsUtilities.ReadAsync(_clientToServerStream, packetData, packetLength);
                        if (!ProcessBodyBytesRead(bytesRead, packetLength, packetType))
                        {
                            return;
                        }
                    }
                    catch (IOException e)
                    {
                        CommunicationsUtilities.Trace(_nodeId, "EXCEPTION in RunPacketReadLoopAsync (Reading): {0}", e);
                        _packetFactory.RoutePacket(_nodeId, new NodeShutdown(NodeShutdownReason.ConnectionFailed));
                        Close();
                        return;
                    }

                    // Read and route the packet.
                    if (!ReadAndRoutePacket(packetType, packetData, packetLength))
                    {
                        return;
                    }

                    if (packetType == NodePacketType.NodeShutdown)
                    {
                        Close();
                        return;
                    }
                }
            }
#endif

            /// <summary>
            /// Sends the specified packet to this node asynchronously.
            /// The method enqueues a task to write the packet and returns
            /// immediately. This is because SendData() is on a hot path
            /// under the primary lock (BuildManager's _syncLock)
            /// and we want to minimize our time there.
            /// </summary>
            /// <param name="packet">The packet to send.</param>
            public void SendData(INodePacket packet)
            {
                if (IsExitPacket(packet))
                {
                    _exitPacketState = ExitPacketState.ExitPacketQueued;
                }
                _packetWriteQueue.Add(packet);
                DrainPacketQueue();
            }

            /// <summary>
            /// Schedule a task to drain the packet write queue. We could have had a
            /// dedicated thread that would pump the queue constantly, but
            /// we don't want to allocate a dedicated thread per node (1MB stack)
            /// </summary>
            /// <remarks>Usually there'll be a single packet in the queue, but sometimes
            /// a burst of SendData comes in, with 10-20 packets scheduled. In this case
            /// the first scheduled task will drain all of them, and subsequent tasks
            /// will run on an empty queue. I tried to write logic that avoids queueing
            /// a new task if the queue is already being drained, but it didn't show any
            /// improvement and made things more complicated.</remarks>
            private void DrainPacketQueue()
            {
                // this lock is only necessary to protect a write to _packetWriteDrainTask field
                lock (_packetWriteQueue)
                {
                    // average latency between the moment this runs and when the delegate starts
                    // running is about 100-200 microseconds (unless there's thread pool saturation)
                    _packetWriteDrainTask = _packetWriteDrainTask.ContinueWith(_ =>
                    {
                        while (_packetWriteQueue.TryTake(out var packet))
                        {
                            SendDataCore(packet);
                        }
                    }, TaskScheduler.Default);
                }
            }

            /// <summary>
            /// Actually writes and sends the packet. This can't be called in parallel
            /// because it reuses the _writeBufferMemoryStream, and this is why we use
            /// the _packetWriteDrainTask to serially chain invocations one after another.
            /// </summary>
            /// <param name="packet">The packet to send.</param>
            private void SendDataCore(INodePacket packet)
            {
                MemoryStream writeStream = _writeBufferMemoryStream;

                // clear the buffer but keep the underlying capacity to avoid reallocations
                writeStream.SetLength(0);

                ITranslator writeTranslator = BinaryTranslator.GetWriteTranslator(writeStream);
                try
                {
                    writeStream.WriteByte((byte)packet.Type);

                    // Pad for the packet length
                    WriteInt32(writeStream, 0);
                    packet.Translate(writeTranslator);

                    int writeStreamLength = (int)writeStream.Position;

                    // Now plug in the real packet length
                    writeStream.Position = 1;
                    WriteInt32(writeStream, writeStreamLength - 5);

                    byte[] writeStreamBuffer = writeStream.GetBuffer();

                    for (int i = 0; i < writeStreamLength; i += MaxPacketWriteSize)
                    {
                        int lengthToWrite = Math.Min(writeStreamLength - i, MaxPacketWriteSize);
                        _serverToClientStream.Write(writeStreamBuffer, i, lengthToWrite);
                    }
                    if (IsExitPacket(packet))
                    {
                        _exitPacketState = ExitPacketState.ExitPacketSent;
                    }
                }
                catch (IOException e)
                {
                    // Do nothing here because any exception will be caught by the async read handler
                    CommunicationsUtilities.Trace(_nodeId, "EXCEPTION in SendData: {0}", e);
                }
                catch (ObjectDisposedException) // This happens if a child dies unexpectedly
                {
                    // Do nothing here because any exception will be caught by the async read handler
                }
            }

            private static bool IsExitPacket(INodePacket packet)
            {
                return packet is NodeBuildComplete buildCompletePacket && !buildCompletePacket.PrepareForReuse;
            }

            /// <summary>
            /// Avoid having a BinaryWriter just to write a 4-byte int
            /// </summary>
            private void WriteInt32(MemoryStream stream, int value)
            {
                stream.WriteByte((byte)value);
                stream.WriteByte((byte)(value >> 8));
                stream.WriteByte((byte)(value >> 16));
                stream.WriteByte((byte)(value >> 24));
            }

            /// <summary>
            /// Closes the node's context, disconnecting it from the node.
            /// </summary>
            private void Close()
            {
                _clientToServerStream.Dispose();
                if (!object.ReferenceEquals(_clientToServerStream, _serverToClientStream))
                {
                    _serverToClientStream.Dispose();
                }
                _terminateDelegate(_nodeId);
            }

            /// <summary>
            /// Waits for the child node process to exit.
            /// </summary>
            public async Task WaitForExitAsync(ILoggingService loggingService)
            {
                if (_exitPacketState == ExitPacketState.ExitPacketQueued)
                {
                    // Wait up to 100ms until all remaining packets are sent.
                    // We don't need to wait long, just long enough for the Task to start running on the ThreadPool.
                    await Task.WhenAny(_packetWriteDrainTask, Task.Delay(100));
                }
                if (_exitPacketState == ExitPacketState.ExitPacketSent)
                {
                    CommunicationsUtilities.Trace("Waiting for node with pid = {0} to exit", _process.Id);

                    // .NET 5 introduces a real WaitForExitAsyc.
                    // This is a poor man's implementation that uses polling.
                    int timeout = TimeoutForWaitForExit;
                    int delay = 5;
                    while (timeout > 0)
                    {
                        bool exited = _process.WaitForExit(milliseconds: 0);
                        if (exited)
                        {
                            return;
                        }
                        timeout -= delay;
                        await Task.Delay(delay).ConfigureAwait(false);

                        // Double delay up to 500ms.
                        delay = Math.Min(delay * 2, 500);
                    }
                }

                // Kill the child and do a blocking wait.
                loggingService?.LogWarning(
                    BuildEventContext.Invalid,
                    null,
                    BuildEventFileInfo.Empty,
                    "KillingProcessWithPid",
                    _process.Id);
                CommunicationsUtilities.Trace("Killing node with pid = {0}", _process.Id);

                _process.KillTree(timeoutMilliseconds: 5000);
            }

#if FEATURE_APM
            /// <summary>
            /// Completes the asynchronous packet write to the node.
            /// </summary>
            private void PacketWriteComplete(IAsyncResult result)
            {
                try
                {
                    _serverToClientStream.EndWrite(result);
                }
                catch (IOException)
                {
                    // Do nothing here because any exception will be caught by the async read handler
                }
            }
#endif

            private bool ProcessHeaderBytesRead(int bytesRead)
            {
                if (bytesRead != _headerByte.Length)
                {
                    CommunicationsUtilities.Trace(_nodeId, "COMMUNICATIONS ERROR (HRC) Node: {0} Process: {1} Bytes Read: {2} Expected: {3}", _nodeId, _process.Id, bytesRead, _headerByte.Length);
                    try
                    {
                        if (_process.HasExited)
                        {
                            CommunicationsUtilities.Trace(_nodeId, "   Child Process {0} has exited.", _process.Id);
                        }
                        else
                        {
                            CommunicationsUtilities.Trace(_nodeId, "   Child Process {0} is still running.", _process.Id);
                        }
                    }
                    catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
                    {
                        CommunicationsUtilities.Trace(_nodeId, "Unable to retrieve remote process information. {0}", e);
                    }

                    _packetFactory.RoutePacket(_nodeId, new NodeShutdown(NodeShutdownReason.ConnectionFailed));
                    Close();
                    return false;
                }

                return true;
            }

#if FEATURE_APM
            /// <summary>
            /// Callback invoked by the completion of a read of a header byte on one of the named pipes.
            /// </summary>
            private void HeaderReadComplete(IAsyncResult result)
            {
                int bytesRead;
                try
                {
                    try
                    {
                        bytesRead = _clientToServerStream.EndRead(result);
                    }

                    // Workaround for CLR stress bug; it sporadically calls us twice on the same async
                    // result, and EndRead will throw on the second one. Pretend the second one never happened.
                    catch (ArgumentException)
                    {
                        CommunicationsUtilities.Trace(_nodeId, "Hit CLR bug #825607: called back twice on same async result; ignoring");
                        return;
                    }

                    if (!ProcessHeaderBytesRead(bytesRead))
                    {
                        return;
                    }
                }
                catch (IOException e)
                {
                    CommunicationsUtilities.Trace(_nodeId, "EXCEPTION in HeaderReadComplete: {0}", e);
                    _packetFactory.RoutePacket(_nodeId, new NodeShutdown(NodeShutdownReason.ConnectionFailed));
                    Close();
                    return;
                }

                int packetLength = BinaryPrimitives.ReadInt32LittleEndian(new Span<byte>(_headerByte, 1, 4));
                MSBuildEventSource.Log.PacketReadSize(packetLength);

                // Ensures the buffer is at least this length.
                // It avoids reallocations if the buffer is already large enough.
                _readBufferMemoryStream.SetLength(packetLength);
                byte[] packetData = _readBufferMemoryStream.GetBuffer();

                _clientToServerStream.BeginRead(packetData, 0, packetLength, BodyReadComplete, new Tuple<byte[], int>(packetData, packetLength));
            }
#endif

            private bool ProcessBodyBytesRead(int bytesRead, int packetLength, NodePacketType packetType)
            {
                if (bytesRead != packetLength)
                {
                    CommunicationsUtilities.Trace(_nodeId, "Bad packet read for packet {0} - Expected {1} bytes, got {2}", packetType, packetLength, bytesRead);
                    _packetFactory.RoutePacket(_nodeId, new NodeShutdown(NodeShutdownReason.ConnectionFailed));
                    Close();
                    return false;
                }
                return true;
            }

            private bool ReadAndRoutePacket(NodePacketType packetType, byte [] packetData, int packetLength)
            {
                try
                {
                    // The buffer is publicly visible so that InterningBinaryReader doesn't have to copy to an intermediate buffer.
                    // Since the buffer is publicly visible dispose right away to discourage outsiders from holding a reference to it.
                    using (var packetStream = new MemoryStream(packetData, 0, packetLength, /*writeable*/ false, /*bufferIsPubliclyVisible*/ true))
                    {
                        ITranslator readTranslator = BinaryTranslator.GetReadTranslator(packetStream, _sharedReadBuffer);
                        _packetFactory.DeserializeAndRoutePacket(_nodeId, packetType, readTranslator);
                    }
                }
                catch (IOException e)
                {
                    CommunicationsUtilities.Trace(_nodeId, "EXCEPTION in ReadAndRoutPacket: {0}", e);
                    _packetFactory.RoutePacket(_nodeId, new NodeShutdown(NodeShutdownReason.ConnectionFailed));
                    Close();
                    return false;
                }
                return true;
            }

#if FEATURE_APM
            /// <summary>
            /// Method called when the body of a packet has been read.
            /// </summary>
            private void BodyReadComplete(IAsyncResult result)
            {
                NodePacketType packetType = (NodePacketType)_headerByte[0];
                var state = (Tuple<byte[], int>)result.AsyncState;
                byte[] packetData = state.Item1;
                int packetLength = state.Item2;
                int bytesRead;

                try
                {
                    try
                    {
                        bytesRead = _clientToServerStream.EndRead(result);
                    }

                    // Workaround for CLR stress bug; it sporadically calls us twice on the same async
                    // result, and EndRead will throw on the second one. Pretend the second one never happened.
                    catch (ArgumentException)
                    {
                        CommunicationsUtilities.Trace(_nodeId, "Hit CLR bug #825607: called back twice on same async result; ignoring");
                        return;
                    }

                    if (!ProcessBodyBytesRead(bytesRead, packetLength, packetType))
                    {
                        return;
                    }
                }
                catch (IOException e)
                {
                    CommunicationsUtilities.Trace(_nodeId, "EXCEPTION in BodyReadComplete (Reading): {0}", e);
                    _packetFactory.RoutePacket(_nodeId, new NodeShutdown(NodeShutdownReason.ConnectionFailed));
                    Close();
                    return;
                }

                // Read and route the packet.
                if (!ReadAndRoutePacket(packetType, packetData, packetLength))
                {
                    return;
                }

                if (packetType != NodePacketType.NodeShutdown)
                {
                    // Read the next packet.
                    BeginAsyncPacketRead();
                }
                else
                {
                    Close();
                }
            }
#endif
        }
    }
}
