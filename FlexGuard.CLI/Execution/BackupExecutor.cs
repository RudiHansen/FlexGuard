using FlexGuard.CLI.Infrastructure;
using FlexGuard.CLI.Reporting;
using FlexGuard.Core.Backup;
using FlexGuard.Core.Config;
using FlexGuard.Core.Models;
using FlexGuard.Core.Options;
using FlexGuard.Core.Recording;
using FlexGuard.Core.Reporting;
using FlexGuard.Core.Util;
using Spectre.Console;
using System.Threading.Tasks;

namespace FlexGuard.CLI.Execution;

public static class BackupExecutor
{
    public static async Task RunAsync(
        ProgramOptions options,
        BackupJobConfig jobConfig,
        IMessageReporter reporter)
    {
        var recorder = Services.Get<BackupRunRecorder>();
        reporter.Info($"Selected Job: {options.JobName}, Operation Mode: {options.Mode}, Compression: {options.Compression}");

        DateTime? lastBackupTime = null;

        if (options.Mode == OperationMode.DifferentialBackup)
        {
            DateTimeOffset? lastDateTimeOffset = await recorder.GetLastJobRunTimeAsync(options.JobName);
            if (lastDateTimeOffset is not null)
            {
                lastBackupTime = lastDateTimeOffset.Value.UtcDateTime;
            }
            else
            {
                reporter.Error("No last backup record found");
                return;
            }
        }

        // We need DestinationFolderName
        string DestinationFolderName = $"{DateTime.UtcNow:yyyy-MM-ddTHHmm}_{GetShortType(options.Mode)}";

        long timerCollectFilesElapsed;
        using var timerCollectFiles = TimingScope.Start();
        var allFiles = FileCollector.CollectFiles(jobConfig, reporter, lastBackupTime);
        if (allFiles.Count <= 0)
        {
            reporter.Info("There are no files to backup!");
            return;
        }
        timerCollectFiles.Stop();
        timerCollectFilesElapsed = (long)timerCollectFiles.Elapsed.TotalMilliseconds;

        // Create the start record for the BackupJob in FlexBackupEntry
        await recorder.StartRunAsync(options.JobName, DestinationFolderName, options.Mode, options.Compression, timerCollectFilesElapsed);

        var totalSize = allFiles.Sum(f => f.FileSize);
        var fileGroups = FileGrouper.GroupFiles(allFiles, options.MaxFilesPerGroup, options.MaxBytesPerGroup);

        string backupFolderPath = Path.Combine(jobConfig.DestinationPath, DestinationFolderName);

        // Setup BackupProgressState to use for showing job progress.
        var progress = new BackupProgressState
        {
            TotalFiles = allFiles.Count,
            TotalBytes = totalSize,
            TotalChunks = fileGroups.Count
        };

        reporter.Info($"Total: {allFiles.Count} files, {FormatHelper.FormatBytes(totalSize)}, {fileGroups.Count} group(s)");
        // Start progress renderer (live console progress)
        using var renderer = new ProgressRenderer(progress);
        renderer.Start();

        // Control maximum parallel chunk jobs
        int maxParallelTasks = options.MaxParallelTasks;

        using var sem = new SemaphoreSlim(maxParallelTasks);

        var tasks = fileGroups.Select(async group =>
        {
            await sem.WaitAsync();
            try
            {
                await ChunkProcessor.ProcessAsync(group, backupFolderPath, reporter, progress, options, recorder);
            }
            finally
            {
                sem.Release();
            }
        });

        await Task.WhenAll(tasks);
        renderer.Stop();

        await recorder.CompleteRunAsync(RunStatus.Completed);
        await recorder.ExportManifestAsync(backupFolderPath);

        reporter.Success("Backup process completed successfully.");
    }

    private static string GetShortType(OperationMode operationMode)
    {
        return operationMode switch
        {
            OperationMode.FullBackup => "FULL",
            OperationMode.DifferentialBackup => "DIFF",
            OperationMode.Restore => "RESTORE", // Will probably not be used, but added for completeness
            _ => "ERROR", // If this used we have a problem
        };
    }
}