﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.Build.FileSystem;
using Microsoft.Build.Framework;
using Microsoft.Build.Graph;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.Experimental.ProjectCache
{
    internal record CacheRequest(BuildSubmission Submission, BuildRequestConfiguration Configuration);

    internal record NullableBool(bool Value)
    {
        public static implicit operator bool(NullableBool? d) => d is not null && d.Value;
    }

    internal enum ProjectCacheServiceState
    {
        NotInitialized,
        BeginBuildStarted,
        BeginBuildFinished,
        ShutdownStarted,
        ShutdownFinished
    }

    internal class ProjectCacheService
    {
        private readonly BuildManager _buildManager;
        private readonly Func<PluginLoggerBase> _loggerFactory;
        private readonly ProjectCacheDescriptor _projectCacheDescriptor;
        private readonly CancellationToken _cancellationToken;
        private readonly ProjectCachePluginBase _projectCachePlugin;
        private ProjectCacheServiceState _serviceState = ProjectCacheServiceState.NotInitialized;

        /// <summary>
        /// An instanatiable version of MSBuildFileSystemBase not overriding any methods,
        /// i.e. falling back to FileSystem.Default.
        /// </summary>
        private sealed class DefaultMSBuildFileSystem : MSBuildFileSystemBase { }

        // Use NullableBool to make it work with Interlock.CompareExchange (doesn't accept bool?).
        // Assume that if one request is a design time build, all of them are.
        // Volatile because it is read by the BuildManager thread and written by one project cache service thread pool thread.
        // TODO: remove after we change VS to set the cache descriptor via build parameters.
        public volatile NullableBool? DesignTimeBuildsDetected;
        private TaskCompletionSource<bool>? LateInitializationForVSWorkaroundCompleted;

        private ProjectCacheService(
            ProjectCachePluginBase projectCachePlugin,
            BuildManager buildManager,
            Func<PluginLoggerBase> loggerFactory,
            ProjectCacheDescriptor projectCacheDescriptor,
            CancellationToken cancellationToken
        )
        {
            _projectCachePlugin = projectCachePlugin;
            _buildManager = buildManager;
            _loggerFactory = loggerFactory;
            _projectCacheDescriptor = projectCacheDescriptor;
            _cancellationToken = cancellationToken;
        }

        public static async Task<ProjectCacheService> FromDescriptorAsync(
            ProjectCacheDescriptor pluginDescriptor,
            BuildManager buildManager,
            ILoggingService loggingService,
            CancellationToken cancellationToken)
        {
            var plugin = await Task.Run(() => GetPluginInstance(pluginDescriptor), cancellationToken)
                .ConfigureAwait(false);

            // TODO: Detect and use the highest verbosity from all the user defined loggers. That's tricky because right now we can't query loggers about
            // their verbosity levels.
            var loggerFactory = new Func<PluginLoggerBase>(() => new LoggingServiceToPluginLoggerAdapter(LoggerVerbosity.Normal, loggingService));

            var service = new ProjectCacheService(plugin, buildManager, loggerFactory, pluginDescriptor, cancellationToken);

            // TODO: remove the if after we change VS to set the cache descriptor via build parameters and always call BeginBuildAsync in FromDescriptorAsync.
            // When running under VS we can't initialize the plugin until we evaluate a project (any project) and extract
            // further information (set by VS) from it required by the plugin.
            if (!pluginDescriptor.VsWorkaround)
            {
                await service.BeginBuildAsync();
            }

            return service;
        }

        // TODO: remove vsWorkaroundOverrideDescriptor after we change VS to set the cache descriptor via build parameters.
        private async Task BeginBuildAsync(ProjectCacheDescriptor? vsWorkaroundOverrideDescriptor = null)
        {
            var logger = _loggerFactory();

            try
            {
                SetState(ProjectCacheServiceState.BeginBuildStarted);

                logger.LogMessage("Initializing project cache plugin", MessageImportance.Low);
                var timer = Stopwatch.StartNew();

                if (_projectCacheDescriptor.VsWorkaround)
                {
                    logger.LogMessage("Running project cache with Visual Studio workaround");
                }

                var projectDescriptor = vsWorkaroundOverrideDescriptor ?? _projectCacheDescriptor;
                await _projectCachePlugin.BeginBuildAsync(
                    new CacheContext(
                        projectDescriptor.PluginSettings,
                        new DefaultMSBuildFileSystem(),
                        projectDescriptor.ProjectGraph,
                        projectDescriptor.EntryPoints),
                    // TODO: Detect verbosity from logging service.
                    logger,
                    _cancellationToken);

                timer.Stop();
                logger.LogMessage($"Finished initializing project cache plugin in {timer.Elapsed.TotalMilliseconds} ms", MessageImportance.Low);

                SetState(ProjectCacheServiceState.BeginBuildFinished);
            }
            catch (Exception e)
            {
                HandlePluginException(e, nameof(ProjectCachePluginBase.BeginBuildAsync));
            }

            if (logger.HasLoggedErrors)
            {
                ProjectCacheException.ThrowForErrorLoggedInsideTheProjectCache("ProjectCacheInitializationFailed");
            }
        }

        private static ProjectCachePluginBase GetPluginInstance(ProjectCacheDescriptor pluginDescriptor)
        {
            if (pluginDescriptor.PluginInstance != null)
            {
                return pluginDescriptor.PluginInstance;
            }
            if (pluginDescriptor.PluginAssemblyPath != null)
            {
                return GetPluginInstanceFromType(GetTypeFromAssemblyPath(pluginDescriptor.PluginAssemblyPath));
            }

            ErrorUtilities.ThrowInternalErrorUnreachable();

            return null!;
        }

        private static ProjectCachePluginBase GetPluginInstanceFromType(Type pluginType)
        {
            try
            {
                return (ProjectCachePluginBase) Activator.CreateInstance(pluginType)!;
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                HandlePluginException(e.InnerException, "Constructor");
            }

            return null!;
        }

        private static Type GetTypeFromAssemblyPath(string pluginAssemblyPath)
        {
            var assembly = LoadAssembly(pluginAssemblyPath);

            var type = GetTypes<ProjectCachePluginBase>(assembly).FirstOrDefault();

            if (type == null)
            {
                ProjectCacheException.ThrowForMSBuildIssueWithTheProjectCache("NoProjectCachePluginFoundInAssembly", pluginAssemblyPath);
            }

            return type!;

            Assembly LoadAssembly(string resolverPath)
            {
#if !FEATURE_ASSEMBLYLOADCONTEXT
                return Assembly.LoadFrom(resolverPath);
#else
                return s_loader.LoadFromPath(resolverPath);
#endif
            }

            IEnumerable<Type> GetTypes<T>(Assembly assembly)
            {
                return assembly.ExportedTypes
                    .Select(type => new {type, info = type.GetTypeInfo()})
                    .Where(
                        t => t.info.IsClass &&
                             t.info.IsPublic &&
                             !t.info.IsAbstract &&
                             typeof(T).IsAssignableFrom(t.type))
                    .Select(t => t.type);
            }
        }

#if FEATURE_ASSEMBLYLOADCONTEXT
        private static readonly CoreClrAssemblyLoader s_loader = new CoreClrAssemblyLoader();
#endif

        public void PostCacheRequest(CacheRequest cacheRequest)
        {
            Task.Run(async () =>
            {
                try
                {
                    var cacheResult = await ProcessCacheRequest(cacheRequest);
                    _buildManager.PostCacheResult(cacheRequest, cacheResult);
                }
                catch (Exception e)
                {
                    _buildManager.PostCacheResult(cacheRequest, CacheResult.IndicateException(e));
                }
            }, _cancellationToken);

            async Task<CacheResult> ProcessCacheRequest(CacheRequest request)
            {
                // Prevent needless evaluation if design time builds detected.
                if (_projectCacheDescriptor.VsWorkaround && DesignTimeBuildsDetected)
                {
                    // The BuildManager should disable the cache when it finds its servicing design time builds.
                    return CacheResult.IndicateNonCacheHit(CacheResultType.CacheMiss);
                }

                EvaluateProjectIfNecessary(request);

                // Detect design time builds.
                if (_projectCacheDescriptor.VsWorkaround)
                {
                    var isDesignTimeBuild = IsDesignTimeBuild(request.Configuration.Project);

                    var previousValue = Interlocked.CompareExchange(
                        ref DesignTimeBuildsDetected,
                        new NullableBool(isDesignTimeBuild),
                        null);

                    ErrorUtilities.VerifyThrowInternalError(
                        previousValue is null || previousValue == false || isDesignTimeBuild,
                        "Either all builds in a build session or design time builds, or none");

                    // No point progressing with expensive plugin initialization or cache query if design time build detected.
                    if (DesignTimeBuildsDetected)
                    {
                        // The BuildManager should disable the cache when it finds its servicing design time builds.
                        return CacheResult.IndicateNonCacheHit(CacheResultType.CacheMiss);
                    }
                }

                // TODO: remove after we change VS to set the cache descriptor via build parameters.
                // VS workaround needs to wait until the first project is evaluated to extract enough information to initialize the plugin.
                // No cache request can progress until late initialization is complete.
                if (_projectCacheDescriptor.VsWorkaround)
                {
                    if (Interlocked.CompareExchange(
                            ref LateInitializationForVSWorkaroundCompleted,
                            new TaskCompletionSource<bool>(),
                            null) is null)
                    {
                        await LateInitializePluginForVsWorkaround(request);
                        LateInitializationForVSWorkaroundCompleted.SetResult(true);
                    }
                    else
                    {
                        // Can't be null. If the thread got here it means another thread initialized the completion source.
                        await LateInitializationForVSWorkaroundCompleted!.Task;
                    }
                }

                ErrorUtilities.VerifyThrowInternalError(
                    LateInitializationForVSWorkaroundCompleted is null ||
                    _projectCacheDescriptor.VsWorkaround && LateInitializationForVSWorkaroundCompleted.Task.IsCompleted,
                    "Completion source should be null when this is not the VS workaround");

                return await GetCacheResultAsync(
                    new BuildRequestData(
                        request.Configuration.Project,
                        request.Submission.BuildRequestData.TargetNames.ToArray()));
            }

            static bool IsDesignTimeBuild(ProjectInstance project)
            {
                var designTimeBuild = project.GetPropertyValue(DesignTimeProperties.DesignTimeBuild);
                var buildingProject = project.GlobalPropertiesDictionary[DesignTimeProperties.BuildingProject]?.EvaluatedValue;

                return MSBuildStringIsTrue(designTimeBuild) ||
                       buildingProject != null && !MSBuildStringIsTrue(buildingProject);
            }

            void EvaluateProjectIfNecessary(CacheRequest request)
            {
                // TODO: only do this if the project cache requests evaluation. QB needs evaluations, but the Anybuild implementation
                // TODO: might not need them, so no point evaluating if it's not necessary. As a caveat, evaluations would still be optimal
                // TODO: when proxy builds are issued by the plugin ( scheduled on the inproc node, no point re-evaluating on out-of-proc nodes).
                lock (request.Configuration)
                {
                    if (!request.Configuration.IsLoaded)
                    {
                        request.Configuration.LoadProjectIntoConfiguration(
                            _buildManager,
                            request.Submission.BuildRequestData.Flags,
                            request.Submission.SubmissionId,
                            Scheduler.InProcNodeId
                        );

                        // If we're taking the time to evaluate, avoid having other nodes to repeat the same evaluation.
                        // Based on the assumption that ProjectInstance serialization is faster than evaluating from scratch.
                        request.Configuration.Project.TranslateEntireState = true;
                    }
                }
            }

            async Task LateInitializePluginForVsWorkaround(CacheRequest request)
            {
                var (_, configuration) = request;
                var solutionPath = configuration.Project.GetPropertyValue(SolutionProjectGenerator.SolutionPathPropertyName);
                var solutionConfigurationXml = configuration.Project.GetPropertyValue(SolutionProjectGenerator.CurrentSolutionConfigurationContents);

                ErrorUtilities.VerifyThrow(
                    solutionPath != null && !string.IsNullOrWhiteSpace(solutionPath) && solutionPath != "*Undefined*",
                    $"Expected VS to set a valid SolutionPath property but got: {solutionPath}");

                ErrorUtilities.VerifyThrow(
                    FileSystems.Default.FileExists(solutionPath),
                    $"Solution file does not exist: {solutionPath}");

                ErrorUtilities.VerifyThrow(
                    string.IsNullOrWhiteSpace(solutionConfigurationXml) is false,
                    "Expected VS to set a xml with all the solution projects' configurations for the currently building solution configuration.");

                // A solution supports multiple solution configurations (different values for Configuration and Platform).
                // Each solution configuration generates a different static graph.
                // Therefore, plugin implementations that rely on creating static graphs in their BeginBuild methods need access to the
                // currently solution configuration used by VS.
                //
                // In this VS workaround, however, we do not have access to VS' solution configuration. The only information we have is a global property
                // named "CurrentSolutionConfigurationContents" that VS sets on each built project. It does not contain the solution configuration itself, but
                // instead it contains information on how the solution configuration maps to each project's configuration.
                //
                // So instead of using the solution file as the entry point, we parse this VS property and extract graph entry points from it, for every project
                // mentioned in the "CurrentSolutionConfigurationContents" global property.
                //
                // Ideally, when the VS workaround is removed from MSBuild and moved into VS, VS should create ProjectGraphDescriptors with the solution path as
                // the graph entrypoint file, and the VS solution configuration as the entry point's global properties.
                var graphEntryPointsFromSolutionConfig = GenerateGraphEntryPointsFromSolutionConfigurationXml(
                    solutionConfigurationXml,
                    configuration.Project);

                await BeginBuildAsync(
                    ProjectCacheDescriptor.FromAssemblyPath(
                        _projectCacheDescriptor.PluginAssemblyPath!,
                        graphEntryPointsFromSolutionConfig,
                        projectGraph: null,
                        _projectCacheDescriptor.PluginSettings));
            }

            static IReadOnlyCollection<ProjectGraphEntryPoint> GenerateGraphEntryPointsFromSolutionConfigurationXml(
                string solutionConfigurationXml,
                ProjectInstance project
            )
            {
                // TODO: fix code clone for parsing CurrentSolutionConfiguration xml: https://github.com/dotnet/msbuild/issues/6751
                var doc = new XmlDocument();
                doc.LoadXml(solutionConfigurationXml);

                var root = doc.DocumentElement!;
                var projectConfigurationNodes = root.GetElementsByTagName("ProjectConfiguration");

                ErrorUtilities.VerifyThrow(projectConfigurationNodes.Count > 0, "Expected at least one project in solution");

                var definingProjectPath = project.FullPath;
                var graphEntryPoints = new List<ProjectGraphEntryPoint>(projectConfigurationNodes.Count);

                var templateGlobalProperties = new Dictionary<string, string>(project.GlobalProperties, StringComparer.OrdinalIgnoreCase);
                RemoveProjectSpecificGlobalProperties(templateGlobalProperties, project);

                foreach (XmlNode node in projectConfigurationNodes)
                {
                    ErrorUtilities.VerifyThrowInternalNull(node.Attributes, nameof(node.Attributes));

                    var buildProjectInSolution = node.Attributes!["BuildProjectInSolution"];
                    if (buildProjectInSolution is not null &&
                        string.IsNullOrWhiteSpace(buildProjectInSolution.Value) is false &&
                        bool.TryParse(buildProjectInSolution.Value, out var buildProject) &&
                        buildProject is false)
                    {
                        continue;
                    }

                    ErrorUtilities.VerifyThrow(
                        node.ChildNodes.OfType<XmlElement>().FirstOrDefault(e => e.Name == "ProjectDependency") is null,
                        "Project cache service does not support solution only dependencies when running under Visual Studio.");

                    var projectPathAttribute = node.Attributes!["AbsolutePath"];
                    ErrorUtilities.VerifyThrow(projectPathAttribute is not null, "Expected VS to set the project path on each ProjectConfiguration element.");

                    var projectPath = projectPathAttribute!.Value;

                    var (configuration, platform) = SolutionFile.ParseConfigurationName(node.InnerText, definingProjectPath, 0, solutionConfigurationXml);

                    // Take the defining project global properties and override the configuration and platform.
                    // It's sufficient to only set Configuration and Platform.
                    // But we send everything to maximize the plugins' potential to quickly workaround potential MSBuild issues while the MSBuild fixes flow into VS.
                    var globalProperties = new Dictionary<string, string>(templateGlobalProperties, StringComparer.OrdinalIgnoreCase)
                    {
                        ["Configuration"] = configuration,
                        ["Platform"] = platform
                    };

                    graphEntryPoints.Add(new ProjectGraphEntryPoint(projectPath, globalProperties));
                }

                return graphEntryPoints;

                // If any project specific property is set, it will propagate down the project graph and force all nodes to that property's specific side effects, which is incorrect.
                void RemoveProjectSpecificGlobalProperties(Dictionary<string, string> globalProperties, ProjectInstance project)
                {
                    // InnerBuildPropertyName is TargetFramework for the managed sdk.
                    var innerBuildPropertyName = ProjectInterpretation.GetInnerBuildPropertyName(project);

                    IEnumerable<string> projectSpecificPropertyNames = new []{innerBuildPropertyName, "Configuration", "Platform", "TargetPlatform", "OutputType"};

                    foreach (var propertyName in projectSpecificPropertyNames)
                    {
                        if (!string.IsNullOrWhiteSpace(propertyName) && globalProperties.ContainsKey(propertyName))
                        {
                            globalProperties.Remove(propertyName);
                        }
                    }
                }
            }

            static bool MSBuildStringIsTrue(string msbuildString) =>
                ConversionUtilities.ConvertStringToBool(msbuildString, nullOrWhitespaceIsFalse: true);
        }

        private async Task<CacheResult> GetCacheResultAsync(BuildRequestData buildRequest)
        {
            lock (this)
            {
                CheckNotInState(ProjectCacheServiceState.NotInitialized);
                CheckNotInState(ProjectCacheServiceState.BeginBuildStarted);

                if (_serviceState is ProjectCacheServiceState.ShutdownStarted or ProjectCacheServiceState.ShutdownFinished)
                {
                    return CacheResult.IndicateNonCacheHit(CacheResultType.CacheNotApplicable);
                }
            }

            ErrorUtilities.VerifyThrowInternalNull(buildRequest.ProjectInstance, nameof(buildRequest.ProjectInstance));

            var queryDescription = $"{buildRequest.ProjectFullPath}" +
                                   $"\n\tTargets:[{string.Join(", ", buildRequest.TargetNames)}]" +
                                   $"\n\tGlobal Properties: {{{string.Join(",", buildRequest.GlobalProperties.Select(kvp => $"{kvp.Name}={kvp.EvaluatedValue}"))}}}";

            var logger = _loggerFactory();

            logger.LogMessage(
                "\n====== Querying project cache for project " + queryDescription,
                MessageImportance.High);

            CacheResult cacheResult = null!;
            try
            {
                cacheResult = await _projectCachePlugin.GetCacheResultAsync(buildRequest, logger, _cancellationToken);
            }
            catch (Exception e)
            {
                HandlePluginException(e, nameof(ProjectCachePluginBase.GetCacheResultAsync));
            }

            if (logger.HasLoggedErrors || cacheResult.ResultType == CacheResultType.None)
            {
                ProjectCacheException.ThrowForErrorLoggedInsideTheProjectCache("ProjectCacheQueryFailed", queryDescription);
            }

            var message = $"------  Plugin result: {cacheResult.ResultType}.";

            switch (cacheResult.ResultType)
            {
                case CacheResultType.CacheHit:
                    message += " Skipping project.";
                    break;
                case CacheResultType.CacheMiss:
                case CacheResultType.CacheNotApplicable:
                    message += " Building project.";
                    break;
                case CacheResultType.None:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            logger.LogMessage(
                message,
                MessageImportance.High);

            return cacheResult;
        }

        public async Task ShutDown()
        {
            var logger = _loggerFactory();

            try
            {
                SetState(ProjectCacheServiceState.ShutdownStarted);

                logger.LogMessage("Shutting down project cache plugin", MessageImportance.Low);
                var timer = Stopwatch.StartNew();

                await _projectCachePlugin.EndBuildAsync(logger, _cancellationToken);

                timer.Stop();
                logger.LogMessage($"Finished shutting down project cache plugin in {timer.Elapsed.TotalMilliseconds} ms", MessageImportance.Low);

                if (logger.HasLoggedErrors)
                {
                    ProjectCacheException.ThrowForErrorLoggedInsideTheProjectCache("ProjectCacheShutdownFailed");
                }
            }
            catch (Exception e) when (e is not ProjectCacheException)
            {
                HandlePluginException(e, nameof(ProjectCachePluginBase.EndBuildAsync));
            }
            finally
            {
                SetState(ProjectCacheServiceState.ShutdownFinished);
            }
        }

        private static void HandlePluginException(Exception e, string apiExceptionWasThrownFrom)
        {
            if (ExceptionHandling.IsCriticalException(e))
            {
                throw e;
            }

            ProjectCacheException.ThrowAsUnhandledException(
                e,
                "ProjectCacheException",
                apiExceptionWasThrownFrom);
        }

        private void SetState(ProjectCacheServiceState newState)
        {
            lock (this)
            {
                switch (newState)
                {
                    case ProjectCacheServiceState.NotInitialized:
                        ErrorUtilities.ThrowInternalError($"Cannot transition to {ProjectCacheServiceState.NotInitialized}");
                        break;
                    case ProjectCacheServiceState.BeginBuildStarted:
                        CheckInState(ProjectCacheServiceState.NotInitialized);
                        break;
                    case ProjectCacheServiceState.BeginBuildFinished:
                        CheckInState(ProjectCacheServiceState.BeginBuildStarted);
                        break;
                    case ProjectCacheServiceState.ShutdownStarted:
                        CheckNotInState(ProjectCacheServiceState.ShutdownStarted);
                        CheckNotInState(ProjectCacheServiceState.ShutdownFinished);
                        break;
                    case ProjectCacheServiceState.ShutdownFinished:
                        CheckInState(ProjectCacheServiceState.ShutdownStarted);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
                }

                _serviceState = newState;
            }
        }

        private void CheckInState(ProjectCacheServiceState expectedState)
        {
            lock (this)
            {
                ErrorUtilities.VerifyThrowInternalError(_serviceState == expectedState, $"Expected state {expectedState}, actual state {_serviceState}");
            }
        }

        private void CheckNotInState(ProjectCacheServiceState unexpectedState)
        {
            lock (this)
            {
                ErrorUtilities.VerifyThrowInternalError(_serviceState != unexpectedState, $"Unexpected state {_serviceState}");
            }
        }

        private class LoggingServiceToPluginLoggerAdapter : PluginLoggerBase
        {
            private readonly ILoggingService _loggingService;

            public override bool HasLoggedErrors { get; protected set; }

            public LoggingServiceToPluginLoggerAdapter(
                LoggerVerbosity verbosity,
                ILoggingService loggingService) : base(verbosity)
            {
                _loggingService = loggingService;
            }

            public override void LogMessage(string message, MessageImportance? messageImportance = null)
            {
                _loggingService.LogCommentFromText(
                    BuildEventContext.Invalid,
                    messageImportance ?? MessageImportance.Normal,
                    message);
            }

            public override void LogWarning(string warning)
            {
                _loggingService.LogWarningFromText(
                    BuildEventContext.Invalid,
                    null,
                    null,
                    null,
                    BuildEventFileInfo.Empty,
                    warning);
            }

            public override void LogError(string error)
            {
                HasLoggedErrors = true;

                _loggingService.LogErrorFromText(
                    BuildEventContext.Invalid,
                    null,
                    null,
                    null,
                    BuildEventFileInfo.Empty,
                    error);
            }
        }
    }
}
