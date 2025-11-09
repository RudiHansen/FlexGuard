using FlexGuard.Core.Reporting;
using Spectre.Console;

namespace FlexGuard.CLI.Reporting;

public class MessageReporterConsole : IMessageReporter
{
    private readonly bool _debugToConsole;
    private readonly bool _debugToFile;
    private readonly string _logFilePath;

    // trådsikring
    private static readonly object _consoleLock = new();
    private static readonly object _fileLock = new();

    public MessageReporterConsole(bool debugToConsole = false, bool debugToFile = true, string? logFilePath = null)
    {
        _debugToConsole = debugToConsole;
        _debugToFile = debugToFile;
        _logFilePath = logFilePath ?? Path.Combine(AppContext.BaseDirectory, "FlexGuard.log");

        lock (_fileLock)
        {
            File.AppendAllText(_logFilePath, $"--- New Session [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ---{Environment.NewLine}");
        }
    }

    public void Info(string message)
    {
        lock (_consoleLock)
        {
            AnsiConsole.MarkupLine($"[white]{Escape(message)}[/]");
        }
        Log("[INFO] " + message);
    }

    public void Verbose(string message)
    {
        lock (_consoleLock)
        {
            AnsiConsole.MarkupLine($"[grey]{Escape(message)}[/]");
        }
        Log("[VERBOSE] " + message);
    }

    public void Success(string message)
    {
        lock (_consoleLock)
        {
            AnsiConsole.MarkupLine($"[green]{Escape(message)}[/]");
        }
        Log("[OK] " + message);
    }

    public void Warning(string message)
    {
        lock (_consoleLock)
        {
            AnsiConsole.MarkupLine($"[yellow]{Escape(message)}[/]");
        }
        Log("[WARN] " + message);
    }

    public void Error(string message)
    {
        lock (_consoleLock)
        {
            AnsiConsole.MarkupLine($"[red]{Escape(message)}[/]");
        }
        Log("[ERROR] " + message);
    }

    public void Error(Exception ex)
    {
        lock (_consoleLock)
        {
            AnsiConsole.MarkupLine($"[red]{Escape(ex.Message)}[/]");
        }
        Log("[ERROR] " + ex.Message);
        if (ex.StackTrace != null)
            Log(ex.StackTrace);
    }

    public void Debug(string message)
    {
        if (_debugToConsole)
        {
            lock (_consoleLock)
            {
                AnsiConsole.MarkupLine($"[blue]{Escape(message)}[/]");
            }
        }

        if (_debugToFile)
            Log("[DEBUG] " + message);
    }

    public void WriteRaw(string message)
    {
        lock (_consoleLock)
        {
            Console.WriteLine(message);
        }
        Log(message);
    }

    // disse to er “valgfri” i din interface – vi lader dem være no-op, men trådsikre
    public void ReportProgress(long currentBytes, long totalBytes, string file)
    {
        // no-op i denne implementation
    }

    public void ReportProgress(long fileSize, string filename)
    {
        // no-op i denne implementation
    }

    private void Log(string message)
    {
        var timestamped = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        lock (_fileLock)
        {
            File.AppendAllText(_logFilePath, timestamped + Environment.NewLine);
        }
    }

    private static string Escape(string input) =>
        input.Replace("[", "[[").Replace("]", "]]");  // Spectre escaping
}