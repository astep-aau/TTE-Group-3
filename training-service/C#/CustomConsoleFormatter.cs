using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

internal class CustomConsoleFormatterOptions : ConsoleFormatterOptions { }

internal class CustomConsoleFormatter : ConsoleFormatter
{
    public CustomConsoleFormatter() : base("customFormatter") { }

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        string message = logEntry.Formatter.Invoke(logEntry.State, logEntry.Exception) ?? string.Empty;
        
        if (string.IsNullOrEmpty(message))
        {
            return;
        }
    
        // Detect and colorize prefixes
        if (message.StartsWith("[C# Controller]:"))
        {
            textWriter.Write("\e[36m[C# Controller]:\e[0m"); // Cyan color
            textWriter.WriteLine(message.AsSpan("[C# Controller]:".Length));
        }
        else if (message.StartsWith("[C# Service]:"))
        {
            textWriter.Write("\e[32m[C# Service]:\e[0m"); // Green color
            textWriter.WriteLine(message.AsSpan("[C# Service]:".Length));
        }
        else
        {
            textWriter.WriteLine(message);
        }
    
        if (logEntry.Exception != null)
        {
            textWriter.WriteLine(logEntry.Exception.ToString());
        }
    }
}