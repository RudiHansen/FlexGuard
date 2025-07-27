using System.Diagnostics;

namespace FlexGuard.Core.Profiling
{
    public class PerformanceScope : IDisposable
    {
        private readonly string _name;
        private readonly PerformanceTracker _tracker;
        private readonly Stopwatch _stopwatch;
        private readonly long _memoryBefore;
        private readonly Dictionary<string, object> _context = new();

        public PerformanceScope(string name, PerformanceTracker tracker)
        {
            _name = name;
            _tracker = tracker;
            _memoryBefore = GC.GetTotalMemory(false);
            _stopwatch = Stopwatch.StartNew();
        }

        public void Set(string key, object value)
        {
            _context[key] = value;
        }

        public void Accumulate(string key, long value)
        {
            if (_context.TryGetValue(key, out var existing) && existing is long current)
                _context[key] = current + value;
            else
                _context[key] = value;
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            long memoryAfter = GC.GetTotalMemory(false);
            _tracker.RecordSection(_name, _stopwatch.Elapsed, _memoryBefore, memoryAfter, _context);
            GC.SuppressFinalize(this);
        }
    }
}