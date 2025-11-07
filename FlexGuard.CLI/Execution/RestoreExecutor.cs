using FlexGuard.CLI.Infrastructure;
using FlexGuard.CLI.Restore;
using FlexGuard.Core.Config;
using FlexGuard.Core.Models;
using FlexGuard.Core.Options;
using FlexGuard.Core.Recording;
using FlexGuard.Core.Reporting;
using FlexGuard.Core.Restore;
using FlexGuard.Core.Util;
using Spectre.Console;

namespace FlexGuard.CLI.Execution;

public static class RestoreExecutor
{
    public static void Run(
        ProgramOptions options,
        BackupJobConfig jobConfig,
        IMessageReporter reporter)
    {
        var selector = new RestoreFileSelector(jobConfig);
        List<FlexBackupFileEntry>? selectedFiles = selector.SelectFiles();
        if (selectedFiles == null || selectedFiles.Count == 0)
        {
            reporter.Warning("No files selected for restore. Exiting.");
            return;
        }
        var totalSize = selectedFiles.Sum(f => f.FileSize);

        reporter.Info("Restore from backup...");
        reporter.Info($"Selected Job: {options.JobName}, Operation Mode: {options.Mode}, Compression: {options.Compression}");
        reporter.Info($"Total: {selectedFiles.Count} files, {FormatHelper.FormatBytes(totalSize)}");

        AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn())
                .Start(async ctx =>
                {
                    var task = ctx.AddTask("Restoring files...", maxValue: totalSize);

                    var reporterWithProgress = new MessageReporterWithProgress(reporter, totalSize,
                        (currentBytes, totalBytes, _) =>
                        {
                            task.Value = currentBytes;
                        });

                    var recorder = Services.Get<BackupRunRecorder>();
                    FlexBackupEntry? backupEntry = await recorder.GetFlexBackupEntryForBackupEntryIdAsync(selectedFiles.First().BackupEntryId);
                    if (backupEntry == null)
                    {
                        reporter.Error($"Backup entry not found for BackupEntryId: {selectedFiles.First().BackupEntryId}");
                        return; // Prevent further null dereference
                    }
                    // cache: chunkId -> chunkEntry
                    var chunkCache = new Dictionary<string, FlexBackupChunkEntry?>();

                    foreach (var file in selectedFiles)
                    {
                        // prøv cache først
                        if (!chunkCache.TryGetValue(file.ChunkEntryId, out var chunkEntry))
                        {
                            // ikke i cache, hent fra recorder
                            chunkEntry = await recorder.GetFlexBackupChunkEntryByIdAsync(file.ChunkEntryId);

                            // læg i cache (også selv om den er null, så vi ikke henter igen)
                            chunkCache[file.ChunkEntryId] = chunkEntry;
                        }

                        if (chunkEntry is null)
                        {
                            reporter.Error("Chunk entry not found for ChunkEntryId: " + file.ChunkEntryId);
                            continue; // Prevent further null dereference
                        }
                        else
                        {
                            string chunkPath = Path.Combine(jobConfig.DestinationPath, backupEntry.DestinationBackupFolder, chunkEntry.ChunkFileName);

                            RestoreHelper.RestoreFile(
                                jobConfig.RestoreTargetFolder,
                                chunkPath,
                                chunkEntry.ChunkHash,
                                file.RelativePath,
                                file.FileSize,
                                file.FileHash,
                                chunkEntry.CompressionMethod,
                                true,
                                reporterWithProgress);
                        }
                    }
                });
        reporter.Success("Restore process completed successfully.");
    }
}