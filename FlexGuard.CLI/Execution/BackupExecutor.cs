using FlexGuard.Core.Backup;
using FlexGuard.Core.Config;
using FlexGuard.Core.Options;
using FlexGuard.Core.Registry;
using FlexGuard.Core.Reporting;
using Spectre.Console;
using System.Threading.Tasks;

namespace FlexGuard.CLI.Execution;

public static class BackupExecutor
{
    public static void Run(
        ProgramOptions options,
        BackupJobConfig jobConfig,
        BackupRegistryManager registryManager,
        IMessageReporter reporter)
    {
        DateTime? lastBackupTime = null;

        if (options.Mode == OperationMode.DifferentialBackup)
        {
            var lastBackupEntry = registryManager.GetLatestEntry();
            lastBackupTime = lastBackupEntry?.TimestampStart;
        }

        var backupEntry = registryManager.AddEntry(DateTime.UtcNow, options.Mode);
        registryManager.Save();

        var allFiles = FileCollector.CollectFiles(jobConfig, reporter, lastBackupTime);
        var totalSize = allFiles.Sum(f => f.FileSize);

        var fileGroups = FileGrouper.GroupFiles(allFiles, options.MaxFilesPerGroup, options.MaxBytesPerGroup);

        var manifestBuilder = new BackupManifestBuilder(
            options.JobName, options.Mode, backupEntry.TimestampStart, options.Compression);

        string backupFolderPath = Path.Combine(jobConfig.DestinationPath, backupEntry.DestinationFolderName);

        AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn())
            .Start(ctx =>
            {
                var task = ctx.AddTask("Backing up files...", maxValue: totalSize);

                var reporterWithProgress = new MessageReporterWithProgress(reporter, totalSize,
                    (currentBytes, totalBytes, _) =>
                    {
                        task.Value = currentBytes;
                    });

                foreach (var group in fileGroups)
                {
                    ChunkProcessor.Process(group, backupFolderPath, reporterWithProgress, manifestBuilder);
                }
            });

        string manifestFileName = manifestBuilder.Save(Path.Combine(AppContext.BaseDirectory, "Jobs", options.JobName));

        backupEntry.TimestampEnd = DateTime.UtcNow;
        registryManager.Save();

        File.Copy(Path.Combine(AppContext.BaseDirectory, "Jobs", options.JobName, manifestFileName),
                  Path.Combine(backupFolderPath, manifestFileName), true);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Jobs", options.JobName, $"registry_{options.JobName}.json"),
                  Path.Combine(backupFolderPath, $"registry_{options.JobName}.json"), true);

        reporter.Success("Backup process completed successfully.");
    }
}