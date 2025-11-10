using FlexGuard.Core.Reporting;
using Spectre.Console;

namespace FlexGuard.CLI.Reporting
{
    /// <summary>
    /// Periodically renders live backup progress to the console using Spectre.Console.
    /// </summary>
    public sealed class ProgressRenderer : IDisposable
    {
        private readonly BackupProgressState _progress;
        private readonly CancellationTokenSource _cts = new();
        private Task? _renderTask;
        private DateTimeOffset _lastRender = DateTimeOffset.MinValue;
        private readonly object _lock = new();

        public ProgressRenderer(BackupProgressState progress)
        {
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        }

        /// <summary>
        /// Starts the renderer in a background task.
        /// </summary>
        public void Start()
        {
            _renderTask = Task.Run(RenderLoopAsync);
        }

        /// <summary>
        /// Stops the renderer and waits for it to complete.
        /// </summary>
        public void Stop()
        {
            _cts.Cancel();
            try { _renderTask?.Wait(1000); } catch { /* ignore */ }
        }

        private async Task RenderLoopAsync()
        {
            // Keep track of last line count so we can overwrite old output
            int lastLines = 0;

            while (!_cts.IsCancellationRequested)
            {
                var snapshot = _progress.Snapshot();

                // Throttle to max 1 update per second
                if ((DateTimeOffset.UtcNow - _lastRender).TotalMilliseconds < 1000)
                {
                    await Task.Delay(200);
                    continue;
                }

                _lastRender = DateTimeOffset.UtcNow;

                lock (_lock)
                {
                    // Move cursor up to overwrite previous progress
                    if (lastLines > 0)
                        AnsiConsole.Cursor.MoveUp(lastLines);

                    // Build formatted output
                    var bar = BuildProgressBar(snapshot.ProgressPercent, 40);

                    var lines = new[]
                    {
                        $"[grey]Progress:[/] {bar}  [yellow]{snapshot.ProgressPercent,6:F1}%[/]",
                        $"Files: [cyan]{snapshot.ProcessedFiles:N0}[/] / [cyan]{snapshot.TotalFiles:N0}[/]",
                        $"Data:  [cyan]{FormatSize(snapshot.ProcessedMB)}[/] / [cyan]{FormatSize(snapshot.TotalMB)}[/]",
                        $"Chunks:[cyan]{snapshot.CompletedChunks}[/] / [cyan]{snapshot.TotalChunks}[/]",
                        $"Speed: [green]{snapshot.SpeedMBs,6:F1} MB/s[/]   " +
                        $"ETA: [yellow]{FormatTime(snapshot.ETA)}[/]   " +
                        $"Elapsed: [grey]{FormatTime(snapshot.Elapsed)}[/]"
                    };

                    foreach (var line in lines)
                        AnsiConsole.MarkupLine(line);

                    lastLines = lines.Length;
                }

                await Task.Delay(1000, _cts.Token);
            }

            // Final snapshot when done
            var final = _progress.Snapshot();
            AnsiConsole.MarkupLine(
                $"[green]Backup complete:[/] {final.ProgressPercent:F1}%  " +
                $"[grey](Elapsed {FormatTime(final.Elapsed)})[/]");
        }

        private static string BuildProgressBar(double percent, int width)
        {
            int filled = (int)Math.Round(percent / 100 * width);
            int empty = width - filled;
            return $"[green]{new string('█', filled)}[/][grey]{new string('─', empty)}[/]";
        }

        private static string FormatTime(TimeSpan ts)
        {
            if (ts == TimeSpan.Zero)
                return "--:--:--";
            return $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
        }

        private static string FormatSize(double mb)
        {
            if (mb < 1024) return $"{mb:F1} MB";
            return $"{mb / 1024:F1} GB";
        }

        public void Dispose() => _cts.Cancel();
    }
}