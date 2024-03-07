using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace MadWizard.ARPergefactor.Logging
{
    internal class CustomLogFormatter() : ConsoleFormatter("arp")
    {
        public override void Write<T>(in LogEntry<T> log, IExternalScopeProvider? scope, TextWriter writer)
        {
            if (log.Category == "Microsoft.Hosting.Lifetime")
                return;

            string? message = log.Formatter?.Invoke(log.State, log.Exception);

            if (message != null)
            {
                message = $"{log.LogLevel.ToShortName()}: {message}";

                if (log.Exception != null)
                {
                    message += $" – {log.Exception}";
                }

                writer.WriteLine(message);

                if (log.Exception != null && log.Exception.StackTrace != null)
                    writer.WriteLine(log.Exception.StackTrace.ToString());
            }
        }
    }

    internal static class LoggingExtensions
    {
        public static string ToShortName(this LogLevel level)
        {
            if (level == LogLevel.Information)
                return "INFO";
            if (level == LogLevel.Warning)
                return "WARN";

            return level.ToString().ToUpper();
        }
    }
}
