using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace FlexGuard.Core.Util
{
    /// <summary>
    /// Measures CPU time, peak CPU utilization, and peak working set for a process
    /// between Start and Stop. Supports multiple concurrent/nested meters.
    /// </summary>
    public sealed class ResourceUsageMeter : IDisposable
    {
        public readonly record struct ResourceUsageResult(
            TimeSpan Duration,
            TimeSpan CpuTime,
            double PeakCpuPercent,
            long PeakWorkingSetBytes,
            long? PeakManagedBytes);

        private readonly Process _process;
        private readonly bool _includeManaged;
        private readonly TimeSpan _sampleInterval;
        private readonly int _cpuCount;

        private readonly Stopwatch _wall = new();
        private readonly CancellationTokenSource _cts = new();

        private Task? _samplerTask;
        private bool _stopped;
        private ResourceUsageResult? _result;

        // Sampling state
        private TimeSpan _cpuAtStart;
        private TimeSpan _cpuPrev;
        private TimeSpan _wallPrev;

        private double _peakCpuPercent;
        private long _peakWorkingSet;
        private long _peakManaged;

        private ResourceUsageMeter(Process process, TimeSpan sampleInterval, bool includeManagedMemory)
        {
            _process = process ?? throw new ArgumentNullException(nameof(process));
            _sampleInterval = sampleInterval <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(100) : sampleInterval;
            _includeManaged = includeManagedMemory;
            _cpuCount = Math.Max(Environment.ProcessorCount, 1);
        }

        /// <summary>
        /// Start a new meter. Defaults to the current process and 100ms sampling interval.
        /// </summary>
        public static ResourceUsageMeter Start(Process? process = null, TimeSpan? sampleInterval = null, bool includeManagedMemory = false)
        {
            var proc = process ?? Process.GetCurrentProcess();
            var meter = new ResourceUsageMeter(proc, sampleInterval ?? TimeSpan.FromMilliseconds(100), includeManagedMemory);
            meter.StartInternal();
            return meter;
        }

        private void StartInternal()
        {
            _cpuAtStart = SafeGetCpu();
            _cpuPrev = _cpuAtStart;
            _wallPrev = TimeSpan.Zero;
            _wall.Restart();

            _samplerTask = Task.Run(SamplerLoopAsync);
        }

        /// <summary>
        /// Stop the meter and return the results (idempotent).
        /// </summary>
        public ResourceUsageResult Stop()
        {
            if (_stopped)
                return _result!.Value;

            _stopped = true;
            _cts.Cancel();

            try { _samplerTask?.Wait(TimeSpan.FromSeconds(2)); } catch { /* swallow */ }

            _wall.Stop();

            var cpuNow = SafeGetCpu();
            var cpuTime = cpuNow - _cpuAtStart;
            if (cpuTime < TimeSpan.Zero) cpuTime = TimeSpan.Zero;

            var duration = _wall.Elapsed;

            var res = new ResourceUsageResult(
                Duration: duration,
                CpuTime: cpuTime,
                PeakCpuPercent: _peakCpuPercent,
                PeakWorkingSetBytes: _peakWorkingSet,
                PeakManagedBytes: _includeManaged ? _peakManaged : null
            );

            _result = res;
            return res;
        }

        public void Dispose()
        {
            if (!_stopped)
                Stop();
            // Do not dispose the Process instance: if it is the current process or shared, caller owns it.
            _cts.Dispose();
        }

        private async Task SamplerLoopAsync()
        {
            var ct = _cts.Token;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var wall = _wall.Elapsed;
                    var cpu = SafeGetCpu();

                    var dWall = wall - _wallPrev;
                    var dCpu = cpu - _cpuPrev;

                    if (dWall > TimeSpan.Zero && dCpu >= TimeSpan.Zero)
                    {
                        var cpuPct = (dCpu.TotalMilliseconds / (dWall.TotalMilliseconds * _cpuCount)) * 100.0;
                        if (cpuPct > _peakCpuPercent) _peakCpuPercent = cpuPct;
                    }

                    var ws = SafeGetWorkingSet();
                    if (ws > _peakWorkingSet) _peakWorkingSet = ws;

                    if (_includeManaged)
                    {
                        // Managed heap size for the current process only.
                        // If measuring a foreign process, this still reflects the current process,
                        // so we only consider it when includeManaged == true and process == current.
                        if (IsCurrentProcess())
                        {
                            var managed = GC.GetTotalMemory(forceFullCollection: false);
                            if (managed > _peakManaged) _peakManaged = managed;
                        }
                    }

                    _wallPrev = wall;
                    _cpuPrev = cpu;
                }
                catch
                {
                    // If the process exits or access fails, just stop sampling.
                    break;
                }

                try { await Task.Delay(_sampleInterval, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { /* normal on stop */ }
            }
        }

        private bool IsCurrentProcess()
        {
            try
            {
                using var current = Process.GetCurrentProcess();
                return _process.Id == current.Id;
            }
            catch { return false; }
        }

        private TimeSpan SafeGetCpu()
        {
            try
            {
                _process.Refresh();
                return _process.TotalProcessorTime;
            }
            catch
            {
                // If the process is gone, return the last known cpu (do not regress)
                return _cpuPrev;
            }
        }

        private long SafeGetWorkingSet()
        {
            try
            {
                _process.Refresh();
                return _process.WorkingSet64;
            }
            catch
            {
                return _peakWorkingSet;
            }
        }
    }
}
