using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace FlexGuard.Core.Profiling
{
    public class PerformanceTracker
    {
        private readonly Dictionary<string, SectionMetrics> _sections = new();
        private readonly List<ChunkMetrics> _chunks = new();

        private TimeSpan _cpuStart;
        private Stopwatch _wallClock;
        private long _memoryStart;

        public void StartGlobal()
        {
            _cpuStart = Process.GetCurrentProcess().TotalProcessorTime;
            _wallClock = Stopwatch.StartNew();
            _memoryStart = GC.GetTotalMemory(false);
        }

        public void EndGlobal()
        {
            _wallClock.Stop();
        }

        public IDisposable TrackSection(string name)
        {
            return new PerformanceScope(name, this);
        }

        public void RegisterChunkMetrics(string name, long originalSize, long compressedSize)
        {
            _chunks.Add(new ChunkMetrics
            {
                ChunkName = name,
                OriginalSize = originalSize,
                CompressedSize = compressedSize
            });
        }

        internal void RecordSection(string name, TimeSpan duration, long memoryBefore, long memoryAfter)
        {
            _sections[name] = new SectionMetrics
            {
                Name = name,
                Duration = duration,
                MemoryBefore = memoryBefore,
                MemoryAfter = memoryAfter
            };
        }

        public PerformanceReport GenerateReport()
        {
            return new PerformanceReport
            {
                TotalWallTime = _wallClock.Elapsed,
                TotalCpuTime = Process.GetCurrentProcess().TotalProcessorTime - _cpuStart,
                MemoryStart = _memoryStart,
                MemoryEnd = GC.GetTotalMemory(false),
                Sections = _sections.Values.ToList(),
                Chunks = _chunks
            };
        }
    }
}
