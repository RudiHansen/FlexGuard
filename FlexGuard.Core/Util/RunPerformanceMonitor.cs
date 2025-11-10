using System.Diagnostics;
using System.Runtime.Versioning;

namespace FlexGuard.Core.Util
{
    /// <summary>
    /// Monitors CPU, disk, memory, and network usage (disk and network only on Windows).
    /// Safe to include in cross-platform builds.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class RunPerformanceMonitor : IDisposable
    {
        private readonly Process _process = Process.GetCurrentProcess();
        private readonly CancellationTokenSource _cts = new();
        private readonly List<(double cpu, double disk, double net, long mem)> _samples = new();

        private readonly bool _isWindows;
        private readonly PerformanceCounter? _diskRead;
        private readonly PerformanceCounter? _diskWrite;
        private readonly List<PerformanceCounter>? _netAdapters;
        private readonly Task? _samplingTask;

        public RunPerformanceMonitor()
        {
            _isWindows = OperatingSystem.IsWindows();

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    if (OperatingSystem.IsWindows())
                    {
                        _diskRead = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
                        _diskWrite = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total");

                        // collect network adapters for combined throughput
                        var netCat = new PerformanceCounterCategory("Network Interface");
                        _netAdapters = netCat.GetInstanceNames()
                            .Select(name => new PerformanceCounter("Network Interface", "Bytes Total/sec", name))
                            .ToList();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PerfMonitor] Counter init failed: {ex.Message}");
                }
            }
                _samplingTask = Task.Run(SampleLoop);
        }
        [SupportedOSPlatform("windows")]
        private async Task SampleLoop()
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

                    double diskMB = 0;
                    double netMB = 0;

                    if (OperatingSystem.IsWindows())
                    {
                        if (_diskRead != null && _diskWrite != null)
                            diskMB = (_diskRead.NextValue() + _diskWrite.NextValue()) / 1_000_000.0;

                        if (_netAdapters != null && _netAdapters.Count > 0)
                            netMB = _netAdapters.Sum(a => a.NextValue()) / 1_000_000.0;
                    }

                    long memMB = _process.WorkingSet64 / 1_000_000;

                    lock (_samples)
                        _samples.Add((cpuPercent, diskMB, netMB, memMB));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PerfMonitor] Sampling error: {ex.Message}");
                }
            }
        }

        public (double CpuAvg, double CpuMax, double DiskAvg, double DiskMax,
                double NetAvg, double NetMax, long MemMax) Stop()
        {
            _cts.Cancel();
            _samplingTask?.Wait(2000);

            lock (_samples)
            {
                if (_samples.Count == 0)
                    return (0, 0, 0, 0, 0, 0, 0);

                return (
                    CpuAvg: _samples.Average(s => s.cpu),
                    CpuMax: _samples.Max(s => s.cpu),
                    DiskAvg: _samples.Average(s => s.disk),
                    DiskMax: _samples.Max(s => s.disk),
                    NetAvg: _samples.Average(s => s.net),
                    NetMax: _samples.Max(s => s.net),
                    MemMax: _samples.Max(s => s.mem)
                );
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _diskRead?.Dispose();
            _diskWrite?.Dispose();
            if (_netAdapters != null)
            {
                foreach (var adapter in _netAdapters)
                    adapter.Dispose();
            }
        }
    }
}