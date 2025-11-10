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

        // --- Smoothing ---
        private double _smoothedSpeed;
        private bool _initialized;
        private TimeSpan? _lastEta;
        private static readonly TimeSpan EtaClamp = TimeSpan.FromMinutes(1);
        private const double Alpha = 0.2; // weight for EMA

        public ProgressRenderer(BackupProgressState progress)
        {
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        }

        public void Start() => _renderTask = Task.Run(RenderLoopAsync);

        public void Stop()
        {
            _cts.Cancel();
            try { _renderTask?.Wait(1000); } catch { /* ignore */ }
        }

        private async Task RenderLoopAsync()
        {
            int lastLines = 0;

            while (!_cts.IsCancellationRequested)
            {
                var snapshot = _progress.Snapshot();

                if ((DateTimeOffset.UtcNow - _lastRender).TotalMilliseconds < 1000)
                {
                    await Task.Delay(200);
                    continue;
                }

                _lastRender = DateTimeOffset.UtcNow;

                lock (_lock)
                {
                    if (lastLines > 0)
                        AnsiConsole.Cursor.MoveUp(lastLines);

                    // --- Smooth speed + ETA ---
                    (double smoothedSpeed, TimeSpan eta) = SmoothSpeedAndEta(
                        snapshot.SpeedMBs, snapshot.TotalMB, snapshot.ProcessedMB);

                    // --- Build output ---
                    int consoleWidth = Math.Max(40, Console.WindowWidth - 25);
                    var bar = BuildProgressBar(snapshot.ProgressPercent, consoleWidth);

                    var lines = new[]
                    {
                        $"[grey]Progress:[/] {bar}  [yellow]{snapshot.ProgressPercent,6:F1}%[/]",
                        $"Files: [cyan]{snapshot.ProcessedFiles:N0}[/] / [cyan]{snapshot.TotalFiles:N0}[/]",
                        $"Data:  [cyan]{FormatSize(snapshot.ProcessedMB)}[/] / [cyan]{FormatSize(snapshot.TotalMB)}[/]",
                        $"Chunks:[cyan]{snapshot.CompletedChunks}[/] / [cyan]{snapshot.TotalChunks}[/]",
                        $"Speed: [green]{smoothedSpeed,6:F1} MB/s[/]   " +
                        $"ETA: [yellow]{FormatTime(eta)}[/]   " +
                        $"Elapsed: [grey]{FormatTime(snapshot.Elapsed)}[/]"
                    };

                    foreach (var line in lines)
                        AnsiConsole.MarkupLine(line);

                    lastLines = lines.Length;
                }

                await Task.Delay(1000, _cts.Token);
            }

            var final = _progress.Snapshot();
            AnsiConsole.MarkupLine(
                $"[green]Backup complete:[/] {final.ProgressPercent:F1}%  " +
                $"[grey](Elapsed {FormatTime(final.Elapsed)})[/]");
        }

        // --- Helpers ---

        private (double speed, TimeSpan eta) SmoothSpeedAndEta(double newSpeed, double totalMB, double processedMB)
        {
            if (!_initialized)
            {
                _smoothedSpeed = newSpeed;
                _initialized = true;
            }
            else
            {
                _smoothedSpeed = (Alpha * newSpeed) + ((1 - Alpha) * _smoothedSpeed);
            }

            var eta = TimeSpan.Zero;
            if (_smoothedSpeed > 0 && processedMB < totalMB)
            {
                double remainingMB = totalMB - processedMB;
                eta = TimeSpan.FromSeconds(remainingMB / _smoothedSpeed);
            }

            // Clamp ETA change
            if (_lastEta != null)
            {
                var diff = eta - _lastEta.Value;
                if (diff > EtaClamp)
                    eta = _lastEta.Value + EtaClamp;
                else if (diff < -EtaClamp)
                    eta = _lastEta.Value - EtaClamp;
            }

            _lastEta = eta;
            return (_smoothedSpeed, eta);
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