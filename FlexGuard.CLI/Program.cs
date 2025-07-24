using FlexGuard.CLI.Restore;
using FlexGuard.Core.Config;
using FlexGuard.Core.Options;
using FlexGuard.Core.Processing;
using FlexGuard.Core.Registry;
using FlexGuard.Core.Reporting;
using FlexGuard.Core.Restore;
using FlexGuard.Core.Util;

class Program
{
    static void Main(string[] args)
    {
        var reporter = new MessageReporterConsole(debugToConsole: true, debugToFile: true);
        reporter.Info("Starting FlexGuard backup...");

        var options = new ProgramOptions("TestSmall", OperationMode.Restore);
        //var options = new ProgramOptions("Test1", OperationMode.FullBackup);
        //var options = new ProgramOptions("TestLarge", OperationMode.FullBackup);
        //var options = new ProgramOptions("TestExLarge", OperationMode.FullBackup);
        reporter.Info($"Selected Job: {options.JobName}, Operation Mode: {options.Mode}");

        BackupJobConfig jobConfig = JobLoader.Load(options.JobName);
        var localJobsFolder = Path.Combine(AppContext.BaseDirectory, "Jobs", options.JobName);

        var registryManager = new BackupRegistryManager(options.JobName, localJobsFolder);

        if (options.Mode == OperationMode.Restore)
        {
            reporter.Info("Restore from backup...");
            var selector = new RestoreFileSelector(registryManager.GetRegistry(), localJobsFolder);
            var selectedFiles = selector.SelectFiles();

            foreach (var file in selectedFiles)
            {
                var chunkPath = Path.Combine(
                    jobConfig.DestinationPath,
                    file.BackupEntry.DestinationFolderName,
                    file.ChunkFile);

                RestoreHelper.RestoreFile(
                    jobConfig.RestoreTargetFolder,
                    chunkPath,
                    file.RelativePath,
                    file.Hash,
                    reporter);
            }

            return;
        }

        var backupEntry = registryManager.AddEntry(DateTime.UtcNow, options.Mode);
        registryManager.Save();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        reporter.Info("Create backup file list...");
        DateTime? lastBackupTime = null;

        if (options.Mode == OperationMode.DifferentialBackup)
        {
            // set lastBackupTime manually for testing purposes
            lastBackupTime = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        var allFiles = FileCollector.CollectFiles(jobConfig, reporter, lastBackupTime);
        FileListReporter.ReportSummary(allFiles, reporter);

        stopwatch.Stop();
        reporter.Info($"Found {allFiles.Count} files to back up.");
        reporter.Info($"Duration: {stopwatch.Elapsed:hh\\:mm\\:ss}");

        stopwatch.Restart();
        reporter.Info("Grouping files into groups...");
        var fileGroups = FileGrouper.GroupFiles(allFiles, options.MaxFilesPerGroup, options.MaxBytesPerGroup,reporter);
        reporter.Info($"Created {fileGroups.Count} file groups.");
        stopwatch.Stop();
        reporter.Info($"Duration: {stopwatch.Elapsed:hh\\:mm\\:ss}");

        var manifestBuilder = new BackupManifestBuilder(options.JobName, options.Mode, backupEntry.TimestampStart);

        stopwatch.Restart();
        reporter.Info("Processing file groups...");
        string backupFolderPath = Path.Combine(jobConfig.DestinationPath, backupEntry.DestinationFolderName);

        int current = 1;
        foreach (var group in fileGroups)
        {
            reporter.Info($"Processing group {current} of {fileGroups.Count} with {group.Files.Count} files ({group.TotalSize / 1024 / 1024} MB)...");
            ChunkProcessor.Process(group, backupFolderPath, options, reporter, manifestBuilder);
            current++;
        }
        stopwatch.Stop();
        reporter.Info($"Processed {fileGroups.Count} groups.");
        reporter.Info($"Duration: {stopwatch.Elapsed:hh\\:mm\\:ss}");

        string manifestFileName = manifestBuilder.Save(localJobsFolder);
        backupEntry.TimestampEnd = DateTime.UtcNow;
        registryManager.Save();

        File.Copy(Path.Combine(localJobsFolder, manifestFileName), Path.Combine(backupFolderPath, manifestFileName), overwrite: true);
        File.Copy(Path.Combine(localJobsFolder, $"registry_{options.JobName}.json"), Path.Combine(backupFolderPath, $"registry_{options.JobName}.json"), overwrite: true);

        reporter.Success("Backup process completed successfully.");
        NotificationHelper.PlayBackupCompleteSound();

    }

}