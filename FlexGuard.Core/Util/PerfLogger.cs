using FlexGuard.Core.Compression;
using FlexGuard.Core.Options;

namespace FlexGuard.Core.Util
{
    public static class PerfLogger
    {
        private static readonly string LogFile = Path.Combine(AppContext.BaseDirectory, "Perf.log");
        private static readonly string[] HeaderLine = { "Timestamp;JobName;OperationMode;CompressionMethod;FileCount;GroupCount;TotalTime" };


        public static void Log(string jobName, OperationMode mode, CompressionMethod compression, int fileCount, int groupCount, TimeSpan totalTime)
        {
            if (!File.Exists(LogFile))
            {
                File.AppendAllLines(LogFile, HeaderLine);
            }

            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss};{jobName};{mode};{compression};{fileCount};{groupCount};{totalTime:hh\\:mm\\:ss}";
            File.AppendAllLines(LogFile, new[] { line });
        }
    }
}
