using Spectre.Console;

namespace FlexGuard.Core.Reporting;

public class MessageReporterConsole : IMessageReporter
{
    private readonly bool _debugToConsole;
    private readonly bool _debugToFile;
    private readonly string _logFilePath;

    public MessageReporterConsole(bool debugToConsole = false, bool debugToFile = true, string? logFilePath = null)
    {
        _debugToConsole = debugToConsole;
        _debugToFile = debugToFile;
        _logFilePath = logFilePath ?? Path.Combine(AppContext.BaseDirectory, "FlexGuard.log");

        File.AppendAllText(_logFilePath, $"--- New Session [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ---{Environment.NewLine}");
    }

    public void Info(string message)
    {
        AnsiConsole.MarkupLine($"[grey][[INFO]][/] {Escape(message)}");
        Log("[INFO] " + message);
    }

    public void Success(string message)
    {
        AnsiConsole.MarkupLine($"[green][[OK]][/] {Escape(message)}");
        Log("[OK] " + message);
    }

    public void Warning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow][[WARN]][/] {Escape(message)}");
        Log("[WARN] " + message);
    }

    public void Error(string message)
    {
        AnsiConsole.MarkupLine($"[red][[ERROR]][/] {Escape(message)}");
        Log("[ERROR] " + message);
    }

    public void Error(Exception ex)
    {
        AnsiConsole.MarkupLine($"[red][[ERROR]][/] {Escape(ex.Message)}");
        Log("[ERROR] " + ex.Message);
        if (ex.StackTrace != null)
            Log(ex.StackTrace);
    }

    public void Debug(string message)
    {
        if (_debugToConsole)
            AnsiConsole.MarkupLine($"[blue][[DEBUG]][/] {Escape(message)}");

        if (_debugToFile)
            Log("[DEBUG] " + message);
    }

    public void WriteRaw(string message)
    {
        Console.WriteLine(message);
        Log(message);
    }

    public void ReportProgress(int current, int total, string file)
    {
        // Optional default implementation (does nothing)
    }

    private void Log(string message)
    {
        var timestamped = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        File.AppendAllText(_logFilePath, timestamped + Environment.NewLine);
    }

    private static string Escape(string input) =>
        input.Replace("[", "[[").Replace("]", "]]");  // Spectre escaping
}
