using System.Diagnostics;

namespace FlexGuard.Core.Util
{
    /// <summary>
    /// Monitors CPU, disk, and memory usage (disk only on Windows).
    /// Safe to include in cross-platform builds.
    /// </summary>
    public sealed class RunPerformanceMonitor : IDisposable
    {
        private readonly Process _process = Process.GetCurrentProcess();
        private readonly CancellationTokenSource _cts = new();
        private readonly List<(double cpu, double disk, long mem)> _samples = new();

        private readonly bool _isWindows;
        private readonly PerformanceCounter? _diskRead;
        private readonly PerformanceCounter? _diskWrite;
        private readonly Task? _samplingTask;

        public RunPerformanceMonitor()
        {
            _isWindows = OperatingSystem.IsWindows();
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    _diskRead = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
                    _diskWrite = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PerfMonitor] Disk counters unavailable: {ex.Message}");
                }
            }

            _samplingTask = Task.Run(SampleLoop);
        }

        private async Task SampleLoop()
        {
            if(OperatingSystem.IsWindows())
            {
                double lastCpuMs = _process.TotalProcessorTime.TotalMilliseconds;

                while (!_cts.IsCancellationRequested)
                {
                    await Task.Delay(1000);

                    try
                    {
                        // CPU %
                        double currentCpuMs = _process.TotalProcessorTime.TotalMilliseconds;
                        double deltaCpuMs = currentCpuMs - lastCpuMs;
                        lastCpuMs = currentCpuMs;
                        double cpuPercent = deltaCpuMs / (Environment.ProcessorCount * 1000.0) * 100.0;

                        // Disk MB/s (only on Windows)
                        double diskMB = 0;
                        if (_isWindows && _diskRead != null && _diskWrite != null)
                        {
                            diskMB = (_diskRead.NextValue() + _diskWrite.NextValue()) / 1_000_000.0;
                        }

                        // Memory MB
                        long memMB = _process.WorkingSet64 / 1_000_000;

                        lock (_samples)
                            _samples.Add((cpuPercent, diskMB, memMB));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[PerfMonitor] Sampling error: {ex.Message}");
                    }
                }
            }
        }

        public (double CpuAvg, double CpuMax, double DiskAvg, double DiskMax, long MemMax) Stop()
        {
            _cts.Cancel();
            _samplingTask?.Wait(2000);

            lock (_samples)
            {
                if (_samples.Count == 0)
                    return (0, 0, 0, 0, 0);

                return (
                    CpuAvg: _samples.Average(s => s.cpu),
                    CpuMax: _samples.Max(s => s.cpu),
                    DiskAvg: _samples.Average(s => s.disk),
                    DiskMax: _samples.Max(s => s.disk),
                    MemMax: _samples.Max(s => s.mem)
                );
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _diskRead?.Dispose();
            _diskWrite?.Dispose();
        }
    }
}