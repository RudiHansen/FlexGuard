using FlexGuard.CLI.Infrastructure;
using FlexGuard.Core.Backup;
using FlexGuard.Core.Config;
using FlexGuard.Core.Manifest;
using FlexGuard.Core.Models;
using FlexGuard.Core.Options;
using FlexGuard.Core.Recording;
using FlexGuard.Core.Reporting;
using FlexGuard.Core.Util;
using Spectre.Console;

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

        if (options.Mode == Core.Options.OperationMode.DifferentialBackup)
        {
            DateTimeOffset? lastDateTimeOffset = await recorder.GetLastJobRunTimeAsync(options.JobName);
            if(lastDateTimeOffset is not null)
            {
                lastBackupTime = lastDateTimeOffset.Value.UtcDateTime;
            }
            else
            {
                reporter.Error("No last backup record found");
                return;
            }
        }

        // We need DesitnationFolderName
        string DestinationFolderName = $"{DateTime.UtcNow:yyyy-MM-ddTHHmm}_{GetShortType(options.Mode)}";

        long timerCollectFilesElapsed;
        using var timerCollectFiles = TimingScope.Start();
        var allFiles = FileCollector.CollectFiles(jobConfig, reporter, lastBackupTime);
        if(allFiles.Count <= 0)
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

        reporter.Info($"Total: {allFiles.Count} files, {FormatHelper.FormatBytes(totalSize)}, {fileGroups.Count} group(s)");

        await AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn())
            .Start(async ctx =>
            {
                var task = ctx.AddTask("Backing up files...", maxValue: totalSize);

                var reporterWithProgress = new MessageReporterWithProgress(reporter, totalSize,
                    (currentBytes, totalBytes, _) =>
                    {
                        task.Value = currentBytes;
                    });

                foreach (var group in fileGroups)
                {
                    await ChunkProcessor.ProcessAsync(group, backupFolderPath, reporterWithProgress ,options, recorder);
                }
            });

        await recorder.CompleteRunAsync(RunStatus.Completed);

        reporter.Success("Backup process completed successfully.");
    }
    private static string GetShortType(OperationMode operationMode)
    {
        return operationMode switch
        {
            OperationMode.FullBackup => "FULL",
            OperationMode.DifferentialBackup => "DIFF",
            OperationMode.Restore => "RESTORE",// Will proberly not be used, but added for completeness
            _ => "ERROR",// If this used we have a problem
        };
    }

}