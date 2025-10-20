using FlexGuard.CLI.Execution;
using FlexGuard.CLI.Options;
using FlexGuard.CLI.Reporting;
using FlexGuard.Core.Config;
using FlexGuard.Core.Options;
using FlexGuard.Core.Profiling;
using FlexGuard.Core.Registry;

namespace FlexGuard.CLI.Entrypoint;

public static class CliEntrypoint
{
    public static void Run(string[] args)
    {
        using var scope = PerformanceTracker.TrackSection("Main");
        var reporter = new MessageReporterConsole(debugToConsole: false, debugToFile: true);
        reporter.Info("Starting FlexGuard backup...");

        ProgramOptions? options = ProgramOptionsParser.Parse(args, reporter);
        if (options == null) return;

        var jobConfig = JobLoader.Load(options.JobName);
        var localJobsFolder = Path.Combine(AppContext.BaseDirectory, "Jobs", options.JobName);
        var registry = new BackupRegistryManager(options.JobName, localJobsFolder);

        switch (options.Mode)
        {
            case OperationMode.Restore:
                RestoreExecutor.Run(options, jobConfig, registry, reporter);
                break;

            case OperationMode.FullBackup:
            case OperationMode.DifferentialBackup:
                BackupExecutor.Run(options, jobConfig, registry, reporter);
                break;
        }
    }
}