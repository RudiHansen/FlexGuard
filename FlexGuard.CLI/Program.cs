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

        var jobConfig = JobLoader.Load(options.JobName);
        var localJobsFolder = Path.Combine(AppContext.BaseDirectory, "Jobs", options.JobName);

        if (options.Mode == OperationMode.Restore)
        {
            reporter.Info("Restore from backup...");
            RestoreHelper.RestoreFile("RDPStarterData\\RDPModel.csv", localJobsFolder, jobConfig, reporter);
            return;
        }

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

        var registryManager = new BackupRegistryManager(options.JobName, localJobsFolder);
        var manifestBuilder = new BackupManifestBuilder(options.JobName, options.Mode);

        stopwatch.Restart();
        reporter.Info("Processing file groups...");
        string backupFolderPath = Path.Combine(jobConfig.DestinationPath, BackupPathHelper.GetBackupFolderName(options.Mode, DateTime.Now));
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
        registryManager.AddEntry(DateTime.UtcNow, options.Mode, manifestFileName, backupFolderPath);
        registryManager.Save();

        reporter.Success("Backup process completed successfully.");
        NotificationHelper.PlayBackupCompleteSound();

    }

}