using Buildalyzer.Environment;
using Buildalyzer;
using Microsoft.Build.Utilities;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.Build.Execution;
using Serilog.Events;

namespace Tool
{
    internal static class DesignTimeBuildHelper
    {
        public static IAnalyzerResult ExecuteDesignTimeBuild(LogSettings settings, FileInfo fileInfo, out IProjectAnalyzer project)
        {
            var buildAnalyzerLogger = CreateLoggerForBuildAnalyzer(settings);
            var analyzerLoggerFactory = new Serilog.Extensions.Logging.SerilogLoggerFactory(buildAnalyzerLogger, false);

            var options = new AnalyzerManagerOptions
            {
                LoggerFactory = analyzerLoggerFactory,
            };

            Log.Logger.Debug("Setting up AnalyzerManager");
            var manager = new AnalyzerManager(options);

            Log.Logger.Information("Getting project from analyzer manager");
            project = manager.GetProject(fileInfo.FullName);
            Log.Logger.Information("Evaluating project in design-time build");
            var environmentOptions = new EnvironmentOptions()
            {
                Restore = false,
                Preference = EnvironmentPreference.Framework
            };
            string msbuildPath = TryGetMsbuildPathFromEnvironment();
            if (msbuildPath != null)
            {
                environmentOptions.EnvironmentVariables.Add("MSBUILD_EXE_PATH", Path.Combine(msbuildPath));
            }

            var designTimeBuildResult = project.Build(environmentOptions);
            Log.Logger.Information("Build result received, success = {OverallSuccess}", designTimeBuildResult.OverallSuccess);

            if (project.ProjectFile.IsMultiTargeted || designTimeBuildResult.Results.Count() > 1)
            {
                Log.Logger.Warning("Project was multi-targeted = {IsMultiTargeted} or result count was greater than 1, only first result used for discovering properties", project.ProjectFile.IsMultiTargeted);
            }

            Log.Logger.Debug("Getting first result from design-time build");
            return designTimeBuildResult.First();
        }

        private static string TryGetMsbuildPathFromEnvironment()
        {
            var msbuildPossiblePaths = new List<string>();

            //var visualStudioInstanceQueryOptions = new VisualStudioInstanceQueryOptions() { DiscoveryTypes = DiscoveryType.DeveloperConsole}
            var msbuildInstances = MSBuildLocator.QueryVisualStudioInstances();

            return msbuildInstances.First().MSBuildPath;
        }

        private static Serilog.Core.Logger CreateLoggerForBuildAnalyzer(LogSettings settings)
        {
            Log.Logger.Debug("Configuring logger for build analyzer");
            var outputTemplate = $"[DesignTimeBuild tid:{{ThreadId}} {{Level:u3}}] {{Message:lj}}{{NewLine}}{{Exception}}";

            var logLevel = settings.LogVerbosity switch
            {
                LogLevel.Debug => LogEventLevel.Debug,
                LogLevel.Warning => LogEventLevel.Warning,
                LogLevel.Info => LogEventLevel.Information,
                _ => throw new NotImplementedException($"{nameof(LogSettings.LogVerbosity)} of value {settings.LogVerbosity} not implemented for setting logger for BuildAnalyzer"),
            };

            var logConfig = new LoggerConfiguration()
                       .MinimumLevel.Is(Serilog.Events.LogEventLevel.Debug)
                       .Enrich.WithProcessId()
                       .Enrich.WithProcessName()
                       .Enrich.WithThreadId()
                       .WriteTo.Console(
                            outputTemplate: outputTemplate,
                            restrictedToMinimumLevel: logLevel);

            if (!string.IsNullOrEmpty(settings.LogFile))
            {
                logConfig = logConfig.WriteTo.File(settings.LogFile, outputTemplate: outputTemplate, shared: true, rollOnFileSizeLimit: true);
            }

            return logConfig.CreateLogger();
        }
    }
}
