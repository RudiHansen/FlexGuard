namespace FlexGuard.Core.Backup;

using FlexGuard.Core.Config;

public interface IBackupStrategy
{
    void RunBackup(BackupConfig config, string destinationPath);
}