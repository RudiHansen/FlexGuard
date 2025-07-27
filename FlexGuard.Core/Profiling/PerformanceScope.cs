using System;
using System.Diagnostics;

namespace FlexGuard.Core.Profiling
{
    public class PerformanceScope : IDisposable
    {
        private readonly string _name;
        private readonly PerformanceTracker _tracker;
        private readonly Stopwatch _stopwatch;
        private readonly long _memoryBefore;

        public PerformanceScope(string name, PerformanceTracker tracker)
        {
            _name = name;
            _tracker = tracker;
            _memoryBefore = GC.GetTotalMemory(false);
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            long memoryAfter = GC.GetTotalMemory(false);
            _tracker.RecordSection(_name, _stopwatch.Elapsed, _memoryBefore, memoryAfter);
            GC.SuppressFinalize(this);
        }
    }
}
