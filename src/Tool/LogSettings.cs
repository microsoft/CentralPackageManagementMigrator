using Serilog;
using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tool
{
    internal class LogSettings : CommandSettings
    {
        [Description("Level of logging output.")]
        [CommandOption("-v|--verbosity")]
        public LogLevel LogVerbosity { get; set; } = LogLevel.Info;

        [Description("Path to log file.")]
        [CommandOption("-l|--log")]
        public string? LogFile { get; set; }
    }

    internal enum LogLevel { Info, Debug, Warning }
}
