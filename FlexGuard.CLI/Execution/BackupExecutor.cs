using FlexGuard.Core.Backup;
using FlexGuard.Core.Config;
using FlexGuard.Core.Options;
using FlexGuard.Core.Registry;
using FlexGuard.Core.Reporting;

namespace FlexGuard.CLI.Execution;

public static class BackupExecutor
{
    public static void Run(
        ProgramOptions options,
        BackupJobConfig jobConfig,
        BackupRegistryManager registryManager,
        IMessageReporter reporter)
    {
        reporter.Info("Create backup file list...");
        DateTime? lastBackupTime = null;

        if (options.Mode == OperationMode.DifferentialBackup)
        {
            var lastBackupEntry = registryManager.GetLatestEntry();
            lastBackupTime = lastBackupEntry?.TimestampStart;
        }

        var backupEntry = registryManager.AddEntry(DateTime.UtcNow, options.Mode);
        registryManager.Save();

        var allFiles = FileCollector.CollectFiles(jobConfig, reporter, lastBackupTime);
        FileListReporter.ReportSummary(allFiles, reporter);
        reporter.Info($"Found {allFiles.Count} files to back up.");

        reporter.Info("Grouping files into groups...");
        var fileGroups = FileGrouper.GroupFiles(allFiles, options.MaxFilesPerGroup, options.MaxBytesPerGroup, reporter);
        reporter.Info($"Created {fileGroups.Count} file groups.");

        var manifestBuilder = new BackupManifestBuilder(
            options.JobName, options.Mode, backupEntry.TimestampStart, options.Compression);

        reporter.Info("Processing file groups...");
        string backupFolderPath = Path.Combine(jobConfig.DestinationPath, backupEntry.DestinationFolderName);

        int current = 1;
        foreach (var group in fileGroups)
        {
            reporter.Info($"Processing group {current} of {fileGroups.Count} with {group.Files.Count} files ({group.TotalSize / 1024 / 1024} MB)...");
            ChunkProcessor.Process(group, backupFolderPath, reporter, manifestBuilder);
            current++;
        }

        reporter.Info($"Processed {fileGroups.Count} groups.");
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
