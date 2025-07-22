using FlexGuard.Core.Options;

namespace FlexGuard.Core.Util;

public static class BackupPathHelper
{
    public static string GetBackupFolderName(OperationMode mode, DateTime now)
    {
        string timestamp = now.ToString("yyyy-MM-ddTHHmm");
        string suffix = mode switch
        {
            OperationMode.FullBackup => "Full",
            OperationMode.DifferentialBackup => "Diff",
            OperationMode.Restore => "Restore",
            _ => "Unknown"
        };
        return $"{timestamp}_{suffix}";
    }
}