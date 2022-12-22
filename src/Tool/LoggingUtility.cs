using Serilog.Sinks.SystemConsole.Themes;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tool
{
    internal static class LoggingUtility
    {
        private static readonly LoggerConfiguration DefaultBaseLoggerConfig = new LoggerConfiguration()
                    .Enrich.WithProcessId()
                    .Enrich.WithProcessName()
                    .Enrich.WithThreadId();

        private static LoggerConfiguration loggerConfig = DefaultBaseLoggerConfig;

        public static LoggerConfiguration BaseLogConfiguration
        {
            get => loggerConfig;
            set => loggerConfig = value ?? loggerConfig;
        }

        public static void SetupLogger(LogSettings settings)
        {
            try
            {
                BaseLogConfiguration = settings.LogVerbosity switch
                {
                    LogLevel.Debug => BaseLogConfiguration.MinimumLevel.Debug(),
                    LogLevel.Info => BaseLogConfiguration.MinimumLevel.Information(),
                    LogLevel.Warning => BaseLogConfiguration.MinimumLevel.Warning(),
                    _ => throw new NotImplementedException($"{nameof(LogSettings.LogVerbosity)} of {settings.LogVerbosity} is not defined for setting up logger"),
                };

                if (!string.IsNullOrEmpty(settings.LogFile))
                {
                    var outputTemplate = "[{Timestamp:HH:mm:ss} {ProcessName}:{ProcessId} {Level:u3} tid:{ThreadId}] {Message:lj}{NewLine}{Exception}";
                    BaseLogConfiguration = BaseLogConfiguration.WriteTo.File(settings.LogFile, outputTemplate: outputTemplate, shared: true, rollOnFileSizeLimit: true);
                }
                
                BaseLogConfiguration = BaseLogConfiguration.WriteTo.Console(theme: AnsiConsoleTheme.Code, outputTemplate: "[{Level:u3}] {Message:l}{NewLine}{Exception}");
                
                Log.Logger = BaseLogConfiguration.CreateLogger();
                Log.Logger.Debug("Logger initalized");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Failed to initalize logger");
                Console.Error.WriteLine(ex.ToString());
                throw;
            }
        }
    }
}
