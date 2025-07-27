// PerformanceScope.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace FlexGuard.Core.Profiling
{
    public class PerformanceScope : IDisposable
    {
        private readonly string _name;
        private readonly Stopwatch _wallClock;
        private readonly long _memoryStart;
        private readonly TimeSpan _cpuStart;
        private readonly Dictionary<string, object> _context = new();

        public PerformanceScope(string name)
        {
            _name = name;
            _wallClock = Stopwatch.StartNew();
            _memoryStart = GC.GetTotalMemory(false);
            _cpuStart = Process.GetCurrentProcess().TotalProcessorTime;
        }

        public void Set(string key, object value)
        {
            _context[key] = value;
        }

        public void Accumulate(string key, long value)
        {
            if (_context.TryGetValue(key, out var current) && current is long currentLong)
                _context[key] = currentLong + value;
            else
                _context[key] = value;
        }

        public void Dispose()
        {
            _wallClock.Stop();
            var wallTime = _wallClock.Elapsed;

            TimeSpan cpuTime = TimeSpan.Zero;
            try
            {
                using var proc = Process.GetCurrentProcess();
                cpuTime = proc.TotalProcessorTime - _cpuStart;
            }
            catch { }

            var cpuPercent = wallTime.TotalMilliseconds > 0
                ? cpuTime.TotalMilliseconds / wallTime.TotalMilliseconds * 100.0
                : 0;

            var sectionEntry = new Dictionary<string, object?>
            {
                ["type"] = "section",
                ["name"] = _name,
                ["wallTime"] = wallTime.ToString(),
                ["cpuTime"] = cpuTime.ToString(),
                ["cpuPercent"] = cpuPercent,
                ["memoryStart"] = _memoryStart,
                ["memoryEnd"] = GC.GetTotalMemory(false),
                ["context"] = _context
            };

            PerformanceTracker.Instance.Log(sectionEntry);
            GC.SuppressFinalize(this);
        }
    }
}
