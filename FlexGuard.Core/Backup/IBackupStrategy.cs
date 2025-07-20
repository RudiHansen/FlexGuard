using FlexGuard.Core.Config;
using FlexGuard.Core.Reporting;

namespace FlexGuard.Core.Backup;
public interface IBackupStrategy
{
    void RunBackup(BackupConfig config, string destinationPath, IMessageReporter reporter);
}