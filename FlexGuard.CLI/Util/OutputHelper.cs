using Spectre.Console;

namespace FlexGuard.CLI.Util
{
    public static class OutputHelper
    {
        private static readonly string _logFilePath = Path.Combine(AppContext.BaseDirectory, "FlexGuard.log");

        private static bool _debugToConsole = false;
        private static bool _debugToFile = true;

        public static void Init(bool debugToConsole = false, bool debugToFile = true)
        {
            _debugToConsole = debugToConsole;
            _debugToFile = debugToFile;

            // Optional: add session separator
            File.AppendAllText(_logFilePath, $"--- New Session [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ---{Environment.NewLine}");
        }

        public static void Info(string message)
        {
            AnsiConsole.MarkupLine($"[grey][[INFO]][/] {Escape(message)}");
            LogToFile("[INFO] " + message);
        }

        public static void Success(string message)
        {
            AnsiConsole.MarkupLine($"[green][[OK]][/] {Escape(message)}");
            LogToFile("[OK] " + message);
        }

        public static void Warning(string message)
        {
            AnsiConsole.MarkupLine($"[yellow][[WARN]][/] {Escape(message)}");
            LogToFile("[WARN] " + message);
        }

        public static void Error(string message)
        {
            AnsiConsole.MarkupLine($"[red][[ERROR]][/] {Escape(message)}");
            LogToFile("[ERROR] " + message);
        }

        public static void Error(Exception ex)
        {
            AnsiConsole.MarkupLine($"[red][[ERROR]][/] {Escape(ex.Message)}");
            LogToFile("[ERROR] " + ex.Message);
            if (ex.StackTrace != null)
                LogToFile(ex.StackTrace);
        }

        public static void Debug(string message)
        {
            if (_debugToConsole)
                AnsiConsole.MarkupLine($"[blue][[DEBUG]][/] {Escape(message)}");

            if (_debugToFile)
                LogToFile("[DEBUG] " + message);
        }

        public static void WriteRaw(string message)
        {
            Console.WriteLine(message);
            LogToFile(message);
        }

        private static void LogToFile(string message)
        {
            var timestamped = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            File.AppendAllText(_logFilePath, timestamped + Environment.NewLine);
        }

        private static string Escape(string input) =>
            input.Replace("[", "[[").Replace("]", "]]");  // Spectre escaping
    }
}
