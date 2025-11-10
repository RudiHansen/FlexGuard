using FlexGuard.Core.Util;
using System.Diagnostics;

namespace POC
{
    public static class RunPerformanceMonitorTest
    {
        /// <summary>
        /// Runs a short stress test to validate RunPerformanceMonitor accuracy.
        /// Simulates CPU load, memory allocation and disk writes for 10 seconds.
        /// </summary>
        public static async Task RunDemoAsync()
        {
            if(OperatingSystem.IsWindows())
            { 
                Console.WriteLine("Starting RunPerformanceMonitor test...");
                using var monitor = new RunPerformanceMonitor();

                // CPU + Memory + Disk simulation
                var cpuTask = SimulateCpuLoadAsync(20);
                var memTask = SimulateMemoryLoadAsync(20);
                var diskTask = SimulateDiskWriteAsync(20);
                var netDiskTask = SimulateNetDiskWriteAsync(20);

                await Task.WhenAll(cpuTask, memTask, diskTask, netDiskTask);

                var (CpuAvg, CpuMax, DiskAvg, DiskMax, NetAvg, NetMax, MemMax) = monitor.Stop();

                Console.WriteLine();
                Console.WriteLine("==== RunPerformanceMonitor Test Results ====");
                Console.WriteLine($"CPU avg:   {CpuAvg:F1}%");
                Console.WriteLine($"CPU max:   {CpuMax:F1}%");
                Console.WriteLine($"Disk avg:  {DiskAvg:F1} MB/s");
                Console.WriteLine($"Disk max:  {DiskMax:F1} MB/s");
                Console.WriteLine($"Net avg:   {NetAvg:F1} MB/s");
                Console.WriteLine($"Net max:   {NetMax:F1} MB/s");
                Console.WriteLine($"Mem max:   {MemMax} MB");
                Console.WriteLine("============================================");
            }
        }

        private static async Task SimulateCpuLoadAsync(int seconds)
        {
            await Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                while (sw.Elapsed < TimeSpan.FromSeconds(seconds))
                {
                    for (int i = 0; i < 5_000_000; i++)
                    {
                        double x = Math.Sin(i) * Math.Cos(i);
                        if (x > 0.9999) { /* just prevent optimization */ }
                    }
                }
            });
        }

        private static async Task SimulateMemoryLoadAsync(int seconds)
        {
            await Task.Run(async () =>
            {
                byte[][] buffers = new byte[10][];
                for (int i = 0; i < buffers.Length; i++)
                    buffers[i] = new byte[10_000_000]; // ~10 MB each

                await Task.Delay(TimeSpan.FromSeconds(seconds));

                // Release memory at end
                buffers = [];
                GC.Collect();
            });
        }

        private static async Task SimulateDiskWriteAsync(int seconds)
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "FlexGuardPerfTest.tmp");
            var data = new byte[4 * 1024 * 1024]; // 4 MB buffer
            new Random().NextBytes(data);

            await Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                using var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None);
                while (sw.Elapsed < TimeSpan.FromSeconds(seconds))
                {
                    fs.Write(data, 0, data.Length);
                    fs.Flush();
                }
            });

            File.Delete(tempFile);
        }
        private static async Task SimulateNetDiskWriteAsync(int seconds)
        {
            string tempFile = Path.Combine(@"G:\FlexGuard", "FlexGuardPerfTest.tmp");
            var data = new byte[4 * 1024 * 1024]; // 4 MB buffer
            new Random().NextBytes(data);

            await Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                using var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None);
                while (sw.Elapsed < TimeSpan.FromSeconds(seconds))
                {
                    fs.Write(data, 0, data.Length);
                    fs.Flush();
                }
            });

            File.Delete(tempFile);
        }

    }
}