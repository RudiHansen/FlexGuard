using FlexGuard.CLI.Infrastructure;
using FlexGuard.CLI.Reporting;
using FlexGuard.Core.Backup;
using FlexGuard.Core.Config;
using FlexGuard.Core.Models;
using FlexGuard.Core.Options;
using FlexGuard.Core.Recording;
using FlexGuard.Core.Reporting;
using FlexGuard.Core.Util;

namespace FlexGuard.CLI.Execution
{
    public static class ImportExecutor
    {
        public static async Task RunAsync(
        ProgramOptions options,
        IMessageReporter reporter)
        {
            reporter.Info($"Import backup data from : {options.ImportBackupDataPath}");
            var recorder = Services.Get<BackupRunRecorder>();
            await recorder.ImportManifestAsync(options.ImportBackupDataPath);
            reporter.Success("Import backup data completed successfully.");
        }
    }
}
