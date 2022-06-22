﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;

using Microsoft.Build.Shared;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Internal;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// The provider for out-of-proc nodes.  This manages the lifetime of external MSBuild.exe processes
    /// which act as child nodes for the build system.
    /// </summary>
    internal class NodeProviderOutOfProc : NodeProviderOutOfProcBase, INodeProvider
    {
        /// <summary>
        /// A mapping of all the nodes managed by this provider.
        /// </summary>
        private Dictionary<int, NodeContext> _nodeContexts;

        /// <summary>
        /// Constructor.
        /// </summary>
        private NodeProviderOutOfProc()
        {
        }

        #region INodeProvider Members

        /// <summary>
        /// Returns the node provider type.
        /// </summary>
        public NodeProviderType ProviderType
        {
            [DebuggerStepThrough]
            get
            { return NodeProviderType.OutOfProc; }
        }

        /// <summary>
        /// Returns the number of available nodes.
        /// </summary>
        public int AvailableNodes
        {
            get
            {
                return ComponentHost.BuildParameters.MaxNodeCount - _nodeContexts.Count;
            }
        }

        /// <summary>
        /// Magic number sent by the host to the client during the handshake.
        /// Derived from the binary timestamp to avoid mixing binary versions,
        /// Is64BitProcess to avoid mixing bitness, and enableNodeReuse to
        /// ensure that a /nr:false build doesn't reuse clients left over from
        /// a prior /nr:true build. The enableLowPriority flag is to ensure that
        /// a build with /low:false doesn't reuse clients left over for a prior
        /// /low:true build.
        /// </summary>
        /// <param name="enableNodeReuse">Is reuse of build nodes allowed?</param>
        /// <param name="enableLowPriority">Is the build running at low priority?</param>
        internal static Handshake GetHandshake(bool enableNodeReuse, bool enableLowPriority)
        {
            CommunicationsUtilities.Trace("MSBUILDNODEHANDSHAKESALT=\"{0}\", msbuildDirectory=\"{1}\", enableNodeReuse={2}, enableLowPriority={3}", Traits.MSBuildNodeHandshakeSalt, BuildEnvironmentHelper.Instance.MSBuildToolsDirectory32, enableNodeReuse, enableLowPriority);
            return new Handshake(CommunicationsUtilities.GetHandshakeOptions(taskHost: false, nodeReuse: enableNodeReuse, lowPriority: enableLowPriority, is64Bit: EnvironmentUtilities.Is64BitProcess));
        }

        /// <summary>
        /// Instantiates a new MSBuild process acting as a child node.
        /// </summary>
        public bool CreateNode(int nodeId, INodePacketFactory factory, NodeConfiguration configuration)
        {
            ErrorUtilities.VerifyThrowArgumentNull(factory, nameof(factory));

            if (_nodeContexts.Count == ComponentHost.BuildParameters.MaxNodeCount)
            {
                ErrorUtilities.ThrowInternalError("All allowable nodes already created ({0}).", _nodeContexts.Count);
                return false;
            }

            // Start the new process.  We pass in a node mode with a node number of 1, to indicate that we
            // want to start up just a standard MSBuild out-of-proc node.
            // Note: We need to always pass /nodeReuse to ensure the value for /nodeReuse from msbuild.rsp
            // (next to msbuild.exe) is ignored.
            string commandLineArgs = $"/nologo /nodemode:1 /nodeReuse:{ComponentHost.BuildParameters.EnableNodeReuse.ToString().ToLower()} /low:{ComponentHost.BuildParameters.LowPriority.ToString().ToLower()}";

            // Make it here.
            CommunicationsUtilities.Trace("Starting to acquire a new or existing node to establish node ID {0}...", nodeId);

            Handshake hostHandshake = new Handshake(CommunicationsUtilities.GetHandshakeOptions(taskHost: false, nodeReuse: ComponentHost.BuildParameters.EnableNodeReuse, lowPriority: ComponentHost.BuildParameters.LowPriority, is64Bit: EnvironmentUtilities.Is64BitProcess));
            NodeContext context = GetNode(null, commandLineArgs, nodeId, factory, hostHandshake, NodeContextTerminated);

            if (context != null)
            {
                _nodeContexts[nodeId] = context;

                // Start the asynchronous read.
                context.BeginAsyncPacketRead();

                // Configure the node.
                context.SendData(configuration);

                return true;
            }

            throw new BuildAbortedException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("CouldNotConnectToMSBuildExe", ComponentHost.BuildParameters.NodeExeLocation));
        }

        /// <summary>
        /// Sends data to the specified node.
        /// </summary>
        /// <param name="nodeId">The node to which data shall be sent.</param>
        /// <param name="packet">The packet to send.</param>
        public void SendData(int nodeId, INodePacket packet)
        {
            ErrorUtilities.VerifyThrow(_nodeContexts.ContainsKey(nodeId), "Invalid node id specified: {0}.", nodeId);

            SendData(_nodeContexts[nodeId], packet);
        }

        /// <summary>
        /// Shuts down all of the connected managed nodes.
        /// </summary>
        /// <param name="enableReuse">Flag indicating if nodes should prepare for reuse.</param>
        public void ShutdownConnectedNodes(bool enableReuse)
        {
            // Send the build completion message to the nodes, causing them to shutdown or reset.
            List<NodeContext> contextsToShutDown;

            lock (_nodeContexts)
            {
                contextsToShutDown = new List<NodeContext>(_nodeContexts.Values);
            }

            ShutdownConnectedNodes(contextsToShutDown, enableReuse);
        }

        /// <summary>
        /// Shuts down all of the managed nodes permanently.
        /// </summary>
        public void ShutdownAllNodes()
        {
            // If no BuildParameters were specified for this build,
            // we must be trying to shut down idle nodes from some
            // other, completed build. If they're still around,
            // they must have been started with node reuse.
            bool nodeReuse = ComponentHost.BuildParameters?.EnableNodeReuse ?? true;

            // To avoid issues with mismatched priorities not shutting
            // down all the nodes on exit, we will attempt to shutdown
            // all matching nodes with and without the priority bit set.
            // This means we need both versions of the handshake.
            ShutdownAllNodes(nodeReuse, NodeContextTerminated);
        }

        #endregion

        #region IBuildComponent Members

        /// <summary>
        /// Initializes the component.
        /// </summary>
        /// <param name="host">The component host.</param>
        public void InitializeComponent(IBuildComponentHost host)
        {
            this.ComponentHost = host;
            _nodeContexts = new Dictionary<int, NodeContext>();
        }

        /// <summary>
        /// Shuts down the component
        /// </summary>
        public void ShutdownComponent()
        {
        }

        #endregion

        /// <summary>
        /// Static factory for component creation.
        /// </summary>
        static internal IBuildComponent CreateComponent(BuildComponentType componentType)
        {
            ErrorUtilities.VerifyThrow(componentType == BuildComponentType.OutOfProcNodeProvider, "Factory cannot create components of type {0}", componentType);
            return new NodeProviderOutOfProc();
        }

        /// <summary>
        /// Method called when a context terminates.
        /// </summary>
        private void NodeContextTerminated(int nodeId)
        {
            lock (_nodeContexts)
            {
                _nodeContexts.Remove(nodeId);
            }
        }
    }
}
