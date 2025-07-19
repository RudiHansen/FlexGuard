using FlexGuard.Core.GroupCompression;
using FlexGuard.Core.Manifest;

namespace FlexGuard.Core.Backup;

public class BackupProcessorGroupFile : IBackupProcessor
{
    private readonly IGroupCompressor _groupCompressor;
    private readonly int _maxFilesPerGroup;
    private readonly long _maxBytesPerGroup;
    private readonly Action<string>? _report;
    private readonly Action<int, int, string>? _reportProgress;

    public BackupProcessorGroupFile(
        IGroupCompressor groupCompressor,
        int maxFilesPerGroup = 100,
        long maxBytesPerGroup = long.MaxValue,
        Action<string>? report = null,
        Action<int, int, string>? reportProgress = null)
    {
        _groupCompressor = groupCompressor;
        _maxFilesPerGroup = maxFilesPerGroup;
        _maxBytesPerGroup = maxBytesPerGroup;
        _report = report;
        _reportProgress = reportProgress;
    }

    public void ProcessFiles(
        IEnumerable<string> files,
        string sourceRoot,
        string destinationFolder,
        List<FileEntry> manifestOut)
    {
        var fileList = files.ToList();
        int totalFiles = fileList.Count;
        int current = 0;
        int groupIndex = 0;

        var currentGroup = new List<string>();
        long currentGroupSize = 0;

        foreach (var file in fileList)
        {
            long fileSize = new FileInfo(file).Length;

            if (currentGroup.Count >= _maxFilesPerGroup || currentGroupSize + fileSize > _maxBytesPerGroup)
            {
                current += WriteGroup(currentGroup, sourceRoot, destinationFolder, groupIndex++, totalFiles, current, manifestOut);
                currentGroup = new List<string>();
                currentGroupSize = 0;
            }

            currentGroup.Add(file);
            currentGroupSize += fileSize;
        }

        if (currentGroup.Count > 0)
        {
            current += WriteGroup(currentGroup, sourceRoot, destinationFolder, groupIndex++, totalFiles, current, manifestOut);
        }
    }

    private int WriteGroup(
        List<string> groupFiles,
        string sourceRoot,
        string destinationFolder,
        int groupIndex,
        int totalFiles,
        int currentStart,
        List<FileEntry> manifestOut)
    {
        string groupName = $"group_{groupIndex + 1:0000}.zip";
        string groupOutputPath = Path.Combine(destinationFolder, groupName);

        _report?.Invoke($"Compressing group {groupIndex + 1} to {groupName} ({groupFiles.Count} files)");

        int localCount = 0;
        var groupResult = _groupCompressor.CompressFiles(groupFiles, groupOutputPath, sourceRoot, file =>
        {
            int current = currentStart + (++localCount);
            _reportProgress?.Invoke(current, totalFiles, file);
        });

        foreach (var item in groupResult)
        {
            manifestOut.Add(new FileEntry
            {
                SourcePath = item.SourcePath,
                RelativePath = item.RelativePath,
                Hash = item.Hash,
                CompressedFileName = groupName
            });
        }

        return groupFiles.Count;
    }
}