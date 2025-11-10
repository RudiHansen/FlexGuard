using FlexGuard.CLI.Execution;
using FlexGuard.CLI.Options;
using FlexGuard.CLI.Reporting;
using FlexGuard.Core.Config;
using FlexGuard.Core.Options;
using FlexGuard.Core.Util;

namespace FlexGuard.CLI.Entrypoint;

public static class CliEntrypoint
{
    public static async Task RunAsync(string[] args)
    {
        var reporter = new MessageReporterConsole(debugToConsole: false, debugToFile: true);
        reporter.Info("Starting FlexGuard backup...");

        RunPerformanceMonitor? monitor = null;
        if (OperatingSystem.IsWindows())
        {
            monitor = new RunPerformanceMonitor();
        }

        ProgramOptions? options = ProgramOptionsParser.Parse(args, reporter);
        if (options == null) return;

        var jobConfig = JobLoader.Load(options.JobName);

        switch (options.Mode)
        {
            case OperationMode.Restore:
                RestoreExecutor.Run(options, jobConfig, reporter);
                break;

            case OperationMode.FullBackup:
            case OperationMode.DifferentialBackup:
                await BackupExecutor.RunAsync(options, jobConfig, reporter);
                break;
        }
        if (OperatingSystem.IsWindows() && monitor != null)
        {
            var (CpuAvg, CpuMax, DiskAvg, DiskMax, NetAvg, NetMax, MemMax) = monitor.Stop();

            reporter.Debug("");
            reporter.Debug("==== RunPerformanceMonitor Test Results ====");
            reporter.Debug($"CPU avg:   {CpuAvg:F1}%");
            reporter.Debug($"CPU max:   {CpuMax:F1}%");
            reporter.Debug($"Disk avg:  {DiskAvg:F1} MB/s");
            reporter.Debug($"Disk max:  {DiskMax:F1} MB/s");
            reporter.Debug($"Net avg:   {NetAvg:F1} MB/s");
            reporter.Debug($"Net max:   {NetMax:F1} MB/s");
            reporter.Debug($"Mem max:   {MemMax} MB");
            reporter.Debug("============================================");
        }
    }
}