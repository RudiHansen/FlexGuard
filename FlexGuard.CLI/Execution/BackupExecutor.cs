using FlexGuard.CLI.Infrastructure;
using FlexGuard.Core.Backup;
using FlexGuard.Core.Config;
using FlexGuard.Core.Manifest;
using FlexGuard.Core.Models;
using FlexGuard.Core.Options;
using FlexGuard.Core.Recording;
using FlexGuard.Core.Registry;
using FlexGuard.Core.Reporting;
using FlexGuard.Core.Util;
using Spectre.Console;

namespace FlexGuard.CLI.Execution;

public static class BackupExecutor
{
    public static async Task RunAsync(
        ProgramOptions options,
        BackupJobConfig jobConfig,
        BackupRegistryManager registryManager,
        IMessageReporter reporter)
    {
        reporter.Info($"Selected Job: {options.JobName}, Operation Mode: {options.Mode}, Compression: {options.Compression}");

        var recorder = Services.Get<BackupRunRecorder>();
        await recorder.StartRunAsync(options.JobName, options.Mode, options.Compression, CancellationToken.None);

        DateTime? lastBackupTime = null;

        if (options.Mode == Core.Options.OperationMode.DifferentialBackup)
        {
            var lastBackupEntry = registryManager.GetLatestEntry();
            lastBackupTime = lastBackupEntry?.TimestampStart;
        }

        var backupEntry = registryManager.AddEntry(DateTime.UtcNow, options.Mode);
        registryManager.Save();

        var allFiles = FileCollector.CollectFiles(jobConfig, reporter, lastBackupTime);
        var totalSize = allFiles.Sum(f => f.FileSize);

        var fileGroups = FileGrouper.GroupFiles(allFiles, options.MaxFilesPerGroup, options.MaxBytesPerGroup);

        var fileManifestBuilder = new FileManifestBuilder(
            options.JobName, options.Mode, backupEntry.TimestampStart, options.Compression);

        var hashManifestBuilder = new HashManifestBuilder(options.JobName, backupEntry.TimestampStart);

        string backupFolderPath = Path.Combine(jobConfig.DestinationPath, backupEntry.DestinationFolderName);

        reporter.Info($"Total: {allFiles.Count} files, {FormatHelper.FormatBytes(totalSize)}, {fileGroups.Count} group(s)");

        AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn())
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
                    ChunkProcessor.Process(group, backupFolderPath, reporterWithProgress, fileManifestBuilder, hashManifestBuilder);
                }
            });

        string fileManifestFileName = fileManifestBuilder.Save(Path.Combine(AppContext.BaseDirectory, "Jobs", options.JobName));
        string hashManifestFileName = hashManifestBuilder.Save(Path.Combine(AppContext.BaseDirectory, "Jobs", options.JobName));

        backupEntry.TimestampEnd = DateTime.UtcNow;
        registryManager.Save();

        File.Copy(Path.Combine(AppContext.BaseDirectory, "Jobs", options.JobName, fileManifestFileName),
                  Path.Combine(backupFolderPath, fileManifestFileName), true);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Jobs", options.JobName, $"registry_{options.JobName}.json"),
                  Path.Combine(backupFolderPath, $"registry_{options.JobName}.json"), true);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Jobs", options.JobName, hashManifestFileName),
                  Path.Combine(backupFolderPath, hashManifestFileName),true);

        await recorder.CompleteRunAsync(RunStatus.Completed, null, CancellationToken.None);

        reporter.Success("Backup process completed successfully.");
    }
}