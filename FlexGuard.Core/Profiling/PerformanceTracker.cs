using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlexGuard.Core.Profiling
{
    public class PerformanceTracker
    {
        private static readonly Lazy<PerformanceTracker> _instance = new(() => new PerformanceTracker());
        public static PerformanceTracker Instance => _instance.Value;
        private DateTime? _globalStartTime;

        private readonly StreamWriter _writer;
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private PerformanceTracker()
        {
            Directory.CreateDirectory("Logs");
            string path = Path.Combine("Logs", $"{DateTime.Now:yyyy-MM-dd_HHmmss}.performance.jsonl");
            _writer = new StreamWriter(path);
        }
        public void StartGlobal() 
        {
            _globalStartTime = DateTime.UtcNow;
        }
        public void EndGlobal()
        {
            var proc = Process.GetCurrentProcess();
            var summary = new
            {
                type = "summary",
                wallTime = proc.StartTime.ToString("O"),
                cpuTime = proc.TotalProcessorTime.ToString(),
                cpuPercent = 0, // placeholder
                memoryStart = 0,
                memoryEnd = GC.GetTotalMemory(false)
            };
            Log(summary);
            _writer.Dispose();
        }
        public TimeSpan GetGlobalElapsed()
        {
            return _globalStartTime.HasValue
                ? DateTime.UtcNow - _globalStartTime.Value
                : TimeSpan.Zero;
        }
        public static PerformanceScope TrackSection(string name) => new(name);

        public void Log(object data)
        {
            string json = JsonSerializer.Serialize(data, _jsonOptions);
            _writer.WriteLine(json);
            _writer.Flush();
        }
    }
}