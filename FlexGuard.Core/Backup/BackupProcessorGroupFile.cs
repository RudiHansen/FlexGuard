using FlexGuard.Core.GroupCompression;
using FlexGuard.Core.Manifest;
using FlexGuard.Core.Reporting;

namespace FlexGuard.Core.Backup;

public class BackupProcessorGroupFile : IBackupProcessor
{
    private readonly IGroupCompressor _groupCompressor;
    private readonly int _maxFilesPerGroup;
    private readonly long _maxBytesPerGroup;
    private readonly IMessageReporter _reporter;

    public BackupProcessorGroupFile(
        IGroupCompressor groupCompressor,
        int maxFilesPerGroup,
        long maxBytesPerGroup,
        IMessageReporter reporter)
    {
        _groupCompressor = groupCompressor;
        _maxFilesPerGroup = maxFilesPerGroup;
        _maxBytesPerGroup = maxBytesPerGroup;
        _reporter = reporter;
    }

    public void ProcessFiles(IEnumerable<string> files, string sourceRoot, string destinationFolder, List<FileEntry> manifestOut, IMessageReporter reporter)
    {
        var fileList = files.ToList();
        int totalFiles = fileList.Count;

        // Grupperingslogik baseret på antal filer og samlet størrelse
        var currentGroup = new List<string>();
        long currentGroupSize = 0;
        int groupIndex = 1;

        foreach (var file in fileList)
        {
            long fileSize = 0;
            try
            {
                fileSize = new FileInfo(file).Length;
            }
            catch (Exception ex)
            {
                _reporter.Warning($"Skipping unreadable file: {file} ({ex.Message})");
                continue;
            }

            if (currentGroup.Count >= _maxFilesPerGroup || (currentGroupSize + fileSize) > _maxBytesPerGroup)
            {
                WriteGroup(groupIndex++, currentGroup, sourceRoot, destinationFolder, manifestOut, totalFiles);
                currentGroup = new List<string>();
                currentGroupSize = 0;
            }

            currentGroup.Add(file);
            currentGroupSize += fileSize;
        }

        if (currentGroup.Count > 0)
        {
            WriteGroup(groupIndex, currentGroup, sourceRoot, destinationFolder, manifestOut, totalFiles);
        }
    }
    private int _currentFileIndex = 0; // Tilføj i klassen

    private void WriteGroup(
    int groupIndex,
    List<string> groupFiles,
    string sourceRoot,
    string destinationFolder,
    List<FileEntry> manifestOut,
    int total)
    {
        string groupName = $"group_{groupIndex:0000}.zip";
        string groupOutputPath = Path.Combine(destinationFolder, groupName);

        _reporter.Info($"Compressing group {groupIndex} to {groupName} ({groupFiles.Count} files)");

        var groupResult = _groupCompressor.CompressFiles(groupFiles, groupOutputPath, sourceRoot, file =>
        {
            _currentFileIndex++;
            _reporter.ReportProgress(_currentFileIndex, total, file);
        });

        foreach (var item in groupResult)
        {
            try
            {
                var info = new FileInfo(item.SourcePath);
                /*
                manifestOut.Add(new FileEntry
                {
                    SourcePath = item.SourcePath,
                    RelativePath = item.RelativePath,
                    Hash = item.Hash,
                    CompressedFileName = groupName,
                    FileSize = info.Length,
                    LastWriteTimeUtc = info.LastWriteTimeUtc
                });
                */
            }
            catch (Exception ex)
            {
                _reporter.Warning($"Could not retrieve file info for metadata: {item.SourcePath} ({ex.Message})");
                // Du kan vælge at fortsætte uden metadata, men her dropper vi entry helt:
                continue;
            }
        }
    }
}