﻿// Copyright (c) 2017 Pavol Kovalik. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PowerBuild
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Management.Automation;
    using System.Threading.Tasks;
    using Logging;
    using Microsoft.Build.CommandLine;
    using Microsoft.Build.Framework;

    /// <summary>
    /// Use MSBuild to build a project.
    /// </summary>
    /// <para type="synopsis">
    /// Use MSBuild to build a project.
    /// </para>
    /// <para type="description">
    /// Builds the specified targets in the project file. If a project file is not specified, MSBuild searches
    /// the current working directory for a file that has a file extension that ends in "proj" and uses that file.
    /// </para>
    /// <example>
    ///   <code>Invoke-MSBuild -Project Project.sln -Target Build -Property @{Configuration=&quot;Release&quot;} -Verbosity Minimal</code>
    /// </example>
    [OutputType(typeof(BuildResult))]
    [Alias("msbuild")]
    [Cmdlet(VerbsLifecycle.Invoke, "MSBuild")]
    public class InvokeMSBuild : PSCmdlet
    {
        private MSBuildHelper _msBuildHelper;

        /// <summary>
        /// Gets or sets Configuration property.
        /// </summary>
        /// <para type="description">
        /// Set build Configuration property.
        /// </para>
        [Parameter(Mandatory = false)]
        [ArgumentCompleter(typeof(ConfigurationArgumentCompleter))]
        public string Configuration { get; set; }

        /// <summary>
        /// Gets or sets console logger to use.
        /// </summary>
        /// <para type="description">
        /// Set console logger to use.
        /// </para>
        [Parameter]
        [Alias("cl")]
        public ConsoleLoggerType? ConsoleLogger { get; set; }

        /// <summary>
        /// Gets or sets the console logger parameters.
        /// </summary>
        /// <para type="description">
        /// Parameters to console logger.
        /// </para>
        [Parameter]
        [Alias("clp")]
        public string ConsoleLoggerParameters { get; set; }

        /// <summary>
        /// Gets or sets detailed summary parameter.
        /// </summary>
        /// <para type="description">
        /// Shows detailed information at the end of the build about the configurations built and how they were scheduled to nodes.
        /// </para>
        [Parameter]
        [Alias("ds")]
        public SwitchParameter DetailedSummary { get; set; }

        /// <summary>
        /// Get or sets project extensions to ignore.
        /// </summary>
        /// <para type="description">
        /// List of extensions to ignore when determining which project file to build.
        /// </para>
        [Parameter]
        [Alias("ignore")]
        public string[] IgnoreProjectExtensions { get; set; }

        /// <summary>
        /// Get or sets logger collection.
        /// </summary>
        /// <para type="description">
        /// Use this loggers to log events from MSBuild.
        /// </para>
        [Parameter]
        [Alias("l")]
        public ILogger[] Logger { get; set; }

        /// <summary>
        /// Gets or sets number of concurrent processes to build with.
        /// </summary>
        /// <para type="description">
        /// Specifies the maximum number of concurrent processes to build with. If the switch is not used,
        /// MSBuild will use up to the number of processors on the computer.
        /// </para>
        [Parameter]
        [Alias("m")]
        [ValidateRange(1, int.MaxValue)]
        public int MaxCpuCount { get; set; } = Environment.ProcessorCount;

        /// <summary>
        /// Gets or sets node reuse.
        /// </summary>
        /// <para type="description">
        /// Enables or Disables the reuse of MSBuild nodes.
        /// </para>
        [Parameter]
        [Alias("nr")]
        public bool? NodeReuse { get; set; } = null;

        /// <summary>
        /// Gets or sets Platform property.
        /// </summary>
        /// <para type="description">
        /// Set build Platform property.
        /// </para>
        [Parameter(Mandatory = false)]
        [ArgumentCompleter(typeof(PlatformArgumentCompleter))]
        public string Platform { get; set; }

        /// <summary>
        /// Gets or sets project to build.
        /// </summary>
        /// <para type="description">
        /// Project to build.
        /// </para>
        [Alias("FullName")]
        [Parameter(
            Position = 0,
            Mandatory = false,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string Project { get; set; }

        /// <summary>
        /// Gets or sets properties.
        /// </summary>
        /// <para type="description">
        /// Set or override these project-level properties.
        /// </para>
        [Alias("p")]
        [Parameter(Mandatory = false)]
        public Hashtable Property { get; set; }

        /// <summary>
        /// Gets or sets targets to build.
        /// </summary>
        /// <para type="description">
        /// Build these targets in the project.
        /// </para>
        [Parameter(Position = 1, Mandatory = false)]
        [Alias("t")]
        [ArgumentCompleter(typeof(TargetArgumentCompleter))]
        public string[] Target { get; set; }

        /// <summary>
        /// Gets or sets tools version.
        /// </summary>
        /// <para type="description">
        /// The version of the MSBuild Toolset (tasks, targets, etc.) to use during build.This version will override
        /// the versions specified by individual projects.
        /// </para>
        [Parameter]
        [ValidateSet("2.0", "3.5", "4.0", "12.0", "14.0", "15.0")]
        [Alias("tv")]
        public string ToolsVersion { get; set; } = InvokeMSBuildParameters.DefaultToolsVersion;

        /// <summary>
        /// Gets or sets logging verbosity.
        /// </summary>
        /// <para type="description">
        /// Display this amount of information in the event log.
        /// </para>
        [Parameter]
        [Alias("v")]
        public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Normal;

        /// <summary>
        /// Gets or sets logging verbosity.
        /// </summary>
        /// <para type="description">
        /// List of warning codes to treats as errors. To treat all warnings as errors use the switch with empty list '-WarningsAsErrors @()'.
        /// </para>
        [Parameter]
        [Alias("err")]
        public string[] WarningsAsErrors { get; set; }

        /// <summary>
        /// Gets or sets logging verbosity.
        /// </summary>
        /// <para type="description">
        /// List of warning codes to treats as low importance messages.
        /// </para>
        [Parameter]
        [Alias("nowarn")]
        public string[] WarningsAsMessages { get; set; }

        protected override void BeginProcessing()
        {
            WriteDebug("Begin processing");
            base.BeginProcessing();
            Factory.InvokeInstance.SetCurrentDirectory(SessionState.Path.CurrentFileSystemLocation.Path);
            _msBuildHelper = Factory.InvokeInstance.CreateMSBuildHelper();
            _msBuildHelper.BeginProcessing();
        }

        protected override void EndProcessing()
        {
            WriteDebug("End processing");
            _msBuildHelper.EndProcessing();
            _msBuildHelper = null;
            base.EndProcessing();
        }

        protected override void ProcessRecord()
        {
            WriteDebug("Process record");

            var properties = Property?.Cast<DictionaryEntry>().ToDictionary(x => x.Key.ToString(), x => x.Value.ToString());
            properties = properties ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var projects = string.IsNullOrEmpty(Project)
                ? new[] { SessionState.Path.CurrentFileSystemLocation.Path }
                : new[] { Project };
            var project = MSBuildApp.ProcessProjectSwitch(projects, IgnoreProjectExtensions, Directory.GetFiles);

            if (!string.IsNullOrEmpty(Configuration))
            {
                properties[nameof(Configuration)] = Configuration;
            }

            if (!string.IsNullOrEmpty(Platform))
            {
                properties[nameof(Platform)] = Platform;
            }

            _msBuildHelper.Parameters = new InvokeMSBuildParameters
            {
                Project = project,
                Verbosity = Verbosity,
                ToolsVersion = ToolsVersion,
                Target = Target,
                MaxCpuCount = MaxCpuCount,
                NodeReuse = NodeReuse ?? Environment.GetEnvironmentVariable("MSBUILDDISABLENODEREUSE") != "1",
                Properties = properties,
                DetailedSummary = DetailedSummary || Verbosity == LoggerVerbosity.Diagnostic,
                WarningsAsErrors = WarningsAsErrors == null
                    ? null
                    : new HashSet<string>(WarningsAsErrors, StringComparer.InvariantCultureIgnoreCase),
                WarningsAsMessages = WarningsAsMessages == null
                    ? null
                    : new HashSet<string>(WarningsAsMessages, StringComparer.InvariantCultureIgnoreCase)
            };

            var loggers = new List<ILogger>();
            ILogger consoleLogger;

            if (Logger != null)
            {
                loggers.AddRange(Logger);
            }

            var consoleLoggerType = GetConsoleLoggerType();

            switch (consoleLoggerType)
            {
                case ConsoleLoggerType.Streams:
                    consoleLogger = Factory.PowerShellInstance.CreateConsoleLogger(Verbosity, ConsoleLoggerParameters, false);
                    break;

                case ConsoleLoggerType.PSHost:
                    consoleLogger = Factory.PowerShellInstance.CreateConsoleLogger(Verbosity, ConsoleLoggerParameters, true);
                    break;

                case ConsoleLoggerType.None:
                    consoleLogger = null;
                    break;

                default:
                    throw new InvalidEnumArgumentException();
            }

            if (consoleLogger != null)
            {
                loggers.Add(consoleLogger);
            }

            var eventSink = new PSEventSink(this);
            foreach (var psLogger in loggers.OfType<IPSLogger>())
            {
                psLogger.Initialize(eventSink);
            }

            var crossDomainLoggers = (
                from unknownLogger in loggers
                group unknownLogger by unknownLogger is MarshalByRefObject
                into marshalByRefLogger
                from logger in MakeLoggersCrossDomain(marshalByRefLogger.Key, marshalByRefLogger)
                select logger).ToArray();

            _msBuildHelper.Loggers = crossDomainLoggers;

            try
            {
                var asyncResults = ProcessRecordAsync(eventSink);
                eventSink.ConsumeEvents();
                var results = asyncResults.Result;
                WriteObject(results, true);
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(ex, "ProcessRecordError", ErrorCategory.NotSpecified, null));
            }
        }

        protected override void StopProcessing()
        {
            WriteDebug("Stop processing");
            _msBuildHelper.StopProcessing();
            _msBuildHelper = null;
            base.StopProcessing();
        }

        private ConsoleLoggerType GetConsoleLoggerType()
        {
            var hasPSLogger = Logger?.Any(x => x is IPSLogger) ?? false;
            return hasPSLogger
                ? ConsoleLogger ?? ConsoleLoggerType.None
                : ConsoleLogger ?? ConsoleLoggerType.Streams;
        }

        private IEnumerable<ILogger> MakeLoggersCrossDomain(bool isMarshalByRef, IEnumerable<ILogger> loggers)
        {
            return isMarshalByRef ? loggers : new[] { new CrossDomainLogger(loggers) };
        }

        private async Task<IEnumerable<BuildResult>> ProcessRecordAsync(PSEventSink eventSink)
        {
            try
            {
                return await MarshalTask.FromAsync(
                    _msBuildHelper.BeginProcessRecord,
                    _msBuildHelper.EndProcessRecord);
            }
            finally
            {
                eventSink.CompleteWriting();
            }
        }
    }
}