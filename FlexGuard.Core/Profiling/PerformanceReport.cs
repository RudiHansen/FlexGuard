using System;
using System.Collections.Generic;

namespace FlexGuard.Core.Profiling
{
    public class PerformanceReport
    {
        public TimeSpan TotalWallTime { get; set; }
        public TimeSpan TotalCpuTime { get; set; }
        public long MemoryStart { get; set; }
        public long MemoryEnd { get; set; }
        public List<SectionMetrics> Sections { get; set; }
        public List<ChunkMetrics> Chunks { get; set; }
    }

    public class SectionMetrics
    {
        public string Name { get; set; }
        public TimeSpan Duration { get; set; }
        public long MemoryBefore { get; set; }
        public long MemoryAfter { get; set; }
    }

    public class ChunkMetrics
    {
        public string ChunkName { get; set; }
        public long OriginalSize { get; set; }
        public long CompressedSize { get; set; }

        public double CompressionRatio => OriginalSize == 0 ? 0 : (1 - (double)CompressedSize / OriginalSize) * 100;
    }
}
