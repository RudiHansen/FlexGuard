using FlexGuard.Core.Manifest;

namespace FlexGuard.Core.Backup;

public interface IBackupProcessor
{
    void ProcessFiles(IEnumerable<string> files, string sourceRoot, string destinationFolder, List<FileEntry> manifestOut);
}