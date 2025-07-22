using FlexGuard.Core.Config;
using FlexGuard.Core.Options;
using FlexGuard.Core.Processing;
using FlexGuard.Core.Registry;
using FlexGuard.Core.Reporting;
using FlexGuard.Core.Util;
using System.Text.RegularExpressions;

class Program
{
    static void Main(string[] args)
    {
        var reporter = new MessageReporterConsole(debugToConsole: true, debugToFile: true);
        reporter.Info("Starting FlexGuard backup...");

        var options = new ProgramOptions("Test1", OperationMode.FullBackup);
        //var options = new ProgramOptions("TestLarge", OperationMode.FullBackup);
        //var options = new ProgramOptions("TestExLarge", OperationMode.FullBackup);
        reporter.Info($"Selected Job: {options.JobName}, Operation Mode: {options.Mode}");

        var jobConfig = JobLoader.Load(options.JobName);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        reporter.Info("Create backup file list...");
        DateTime? lastBackupTime = null;

        if (options.Mode == OperationMode.DifferentialBackup)
        {
            // set lastBackupTime manually for testing purposes
            lastBackupTime = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        var allFiles = FileCollector.CollectFiles(jobConfig, reporter, lastBackupTime);

        stopwatch.Stop();
        reporter.Info($"Found {allFiles.Count} files to back up.");
        reporter.Info($"Duration: {stopwatch.Elapsed:hh\\:mm\\:ss}");

        stopwatch.Restart();
        reporter.Info("Grouping files into chunks...");
        var fileGroups = ChunkBuilder.BuildGroups(allFiles, options);
        reporter.Info($"Created {fileGroups.Count} file groups.");
        stopwatch.Stop();
        reporter.Info($"Duration: {stopwatch.Elapsed:hh\\:mm\\:ss}");

        var registryManager = new BackupRegistryManager(options.JobName, Path.Combine(jobConfig.DestinationPath, options.JobName));

        stopwatch.Restart();
        reporter.Info("Processing file groups...");
        string backupFolderPath = Path.Combine(jobConfig.DestinationPath, BackupPathHelper.GetBackupFolderName(options.Mode, DateTime.Now));
        int current = 1;
        foreach (var group in fileGroups)
        {
            reporter.Info($"Processing group {current} of {fileGroups.Count} with {group.Files.Count} files ({group.TotalSize / 1024 / 1024} MB)...");
            ChunkProcessor.Process(group, backupFolderPath, options, reporter);
            current++;
        }
        stopwatch.Stop();
        reporter.Info($"Processed {fileGroups.Count} groups.");
        reporter.Info($"Duration: {stopwatch.Elapsed:hh\\:mm\\:ss}");
        registryManager.AddEntry(DateTime.Now, options.Mode, "manifestfilnavn.json");
        registryManager.Save();

        reporter.Success("Backup process completed successfully.");

    }

}