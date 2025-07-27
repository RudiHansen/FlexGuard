using System.Diagnostics;
using System.Text.Json;

namespace FlexGuard.Core.Profiling
{
    public class PerformanceTracker
    {
        private static PerformanceTracker? _instance;
        public static PerformanceTracker Instance => _instance ??= new PerformanceTracker();

        private readonly Dictionary<string, SectionMetrics> _sections = new();
        private readonly List<ChunkMetrics> _chunks = new();

        private TimeSpan _cpuStart;
        private Stopwatch _wallClock = null!;
        private long _memoryStart;

        private StreamWriter? _logWriter;
        private readonly string _logFilePath = $"Logs/{DateTime.Now:yyyy-MM-dd_HHmmss}.performance.jsonl";
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        public void StartGlobal()
        {
            Directory.CreateDirectory("Logs");
            _logWriter = new StreamWriter(_logFilePath, append: true);

            _cpuStart = Process.GetCurrentProcess().TotalProcessorTime;
            _wallClock = Stopwatch.StartNew();
            _memoryStart = GC.GetTotalMemory(false);
        }

        public void EndGlobal()
        {
            _wallClock.Stop();

            var cpuTime = Process.GetCurrentProcess().TotalProcessorTime - _cpuStart;
            var wallTime = _wallClock.Elapsed;
            var cpuPercent = wallTime.TotalSeconds > 0 ? (cpuTime.TotalSeconds / wallTime.TotalSeconds) * 100 : 0;
            var memoryEnd = GC.GetTotalMemory(false);

            Log(new
            {
                type = "summary",
                wallTime = wallTime.ToString(),
                cpuTime = cpuTime.ToString(),
                cpuPercent,
                memoryStart = _memoryStart,
                memoryEnd
            });

            _logWriter?.Dispose();
        }

        public PerformanceScope TrackSection(string name)
        {
            return new PerformanceScope(name, this);
        }

        public void RegisterChunkMetrics(string name, long originalSize, long compressedSize)
        {
            var chunk = new ChunkMetrics
            {
                ChunkName = name,
                OriginalSize = originalSize,
                CompressedSize = compressedSize
            };
            _chunks.Add(chunk);

            Log(new
            {
                type = "chunk",
                chunkName = name,
                originalSize,
                compressedSize
            });
        }

        internal void RecordSection(string name, TimeSpan duration, long memoryBefore, long memoryAfter, Dictionary<string, object> context)
        {
            var section = new SectionMetrics
            {
                Name = name,
                Duration = duration,
                MemoryBefore = memoryBefore,
                MemoryAfter = memoryAfter
            };
            _sections[name] = section;

            Log(new
            {
                type = "section",
                name,
                duration = duration.ToString(),
                memoryBefore,
                memoryAfter,
                context
            });
        }

        private void Log(object record)
        {
            if (_logWriter == null) return;
            string json = JsonSerializer.Serialize(record, _jsonOptions);
            _logWriter.WriteLine(json);
            _logWriter.Flush();
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