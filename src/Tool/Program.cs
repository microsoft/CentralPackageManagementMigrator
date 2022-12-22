using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.Threading.Tasks;

namespace Tool
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var programArgs = string.Join(" ", args ?? Array.Empty<string>());
            var app = new CommandApp<MigratorCommand>();

            var runAppTask = app.RunAsync(args ?? Array.Empty<string>());
            var programResult = await runAppTask;
            ShutdownProgram(programResult, runAppTask.Exception);

            await Task.FromResult(0); 
        }

        private static void ShutdownProgram(int programResult, AggregateException? exception)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine();

            if (exception != null)
            {
                if (exception.InnerExceptions != null)
                {
                    // only show inner exceptions
                    foreach (var innerException in exception.InnerExceptions)
                    {
                        var exceptionHeader = new Rule("[red]Exception Encountered[/]");
                        exceptionHeader.Style = Style.Parse("red dim");
                        exceptionHeader.Alignment = Justify.Center;
                        AnsiConsole.Write(exceptionHeader);
                        AnsiConsole.WriteException(innerException);
                    }
                }
                else
                {
                    var exceptionHeader = new Rule("[red]Exception Encountered[/]");
                    exceptionHeader.Style = Style.Parse("red dim");
                    exceptionHeader.Alignment = Justify.Center;
                    AnsiConsole.Write(exceptionHeader);
                    AnsiConsole.WriteException(exception);
                }
            }

            Log.Information("CentralPackageManagementMigrator complete, exit code {ProgramResult}", programResult);
            FlushLogger();

            Environment.Exit(programResult);
        }

        private static void FlushLogger()
        {
            try
            {
                Log.CloseAndFlush();
            }
            catch
            {
                // ignore
            }
        }
    }
}
