using System.Diagnostics;

namespace FlexGuard.Core.Util
{
    /// <summary>
    /// Lightweight timing scope. Start → do work → Stop/Dispose → read Elapsed.
    /// </summary>
    public sealed class TimingScope : IDisposable
    {
        private readonly Stopwatch _sw;
        private bool _stopped;

        private TimingScope()
        {
            _sw = Stopwatch.StartNew();
        }

        public static TimingScope Start() => new TimingScope();

        /// <summary>Stop the timer (idempotent).</summary>
        public void Stop()
        {
            if (_stopped) return;
            _sw.Stop();
            _stopped = true;
        }

        /// <summary>Current elapsed time.</summary>
        public TimeSpan Elapsed => _sw.Elapsed;

        /// <summary>Allows using-pattern: using var t = TimingScope.Start();</summary>
        public void Dispose() => Stop();

        // Convenience: measure an Action
        public static TimeSpan Measure(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            var sw = Stopwatch.StartNew();
            action();
            sw.Stop();
            return sw.Elapsed;
        }

        // Convenience: measure an async Func<Task>
        public static async Task<TimeSpan> MeasureAsync(Func<Task> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            var sw = Stopwatch.StartNew();
            await action().ConfigureAwait(false);
            sw.Stop();
            return sw.Elapsed;
        }
    }
}
