using FlexGuard.CLI.Restore;
using FlexGuard.Core.Config;
using FlexGuard.Core.Options;
using FlexGuard.Core.Registry;
using FlexGuard.Core.Reporting;
using FlexGuard.Core.Restore;

namespace FlexGuard.CLI.Execution;

public static class RestoreExecutor
{
    public static void Run(
        ProgramOptions options,
        BackupJobConfig jobConfig,
        BackupRegistryManager registryManager,
        IMessageReporter reporter)
    {
        reporter.Info("Restore from backup...");

        var selector = new RestoreFileSelector(registryManager.GetRegistry(), Path.Combine(AppContext.BaseDirectory, "Jobs", options.JobName));
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
                file.Compression,
                reporter);
        }
        reporter.Success("Restore process completed successfully.");
    }
}