using FlexGuard.Core.Manifest;
using FlexGuard.Core.Reporting;

namespace FlexGuard.Core.Backup;

public interface IBackupProcessor
{
    void ProcessFiles(
        IEnumerable<string> files,
        string sourceRoot,
        string destinationFolder,
        List<FileEntry> manifestOut,
        IMessageReporter reporter);
}