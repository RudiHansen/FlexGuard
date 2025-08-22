using FlexGuard.CLI.Restore;
using FlexGuard.Core.Config;
using FlexGuard.Core.Options;
using FlexGuard.Core.Registry;
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
        BackupRegistryManager registryManager,
        IMessageReporter reporter)
    {
        var selector = new RestoreFileSelector(registryManager.GetRegistry(), Path.Combine(AppContext.BaseDirectory, "Jobs", options.JobName));
        var selectedFiles = selector.SelectFiles();
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
                .Start(ctx =>
                {
                    var task = ctx.AddTask("Restoring files...", maxValue: totalSize);

                    var reporterWithProgress = new MessageReporterWithProgress(reporter, totalSize,
                        (currentBytes, totalBytes, _) =>
                        {
                            task.Value = currentBytes;
                        });

                    foreach (var file in selectedFiles)
                    {
                        var chunkPath = Path.Combine(
                            jobConfig.DestinationPath,
                            file.BackupEntry.DestinationFolderName,
                            file.ChunkFile);

                        RestoreHelper.RestoreFile(
                            jobConfig.RestoreTargetFolder,
                            chunkPath,
                            file.ChunkHash,
                            file.RelativePath,
                            file.FileSize,
                            file.FileHash,
                            file.Compression,
                            file.CompressionSkipped,
                            reporterWithProgress);
                    }
                });
        reporter.Success("Restore process completed successfully.");
    }
}