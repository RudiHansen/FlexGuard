using FlexGuard.Core.GroupCompression;
using FlexGuard.Core.Manifest;

namespace FlexGuard.Core.Backup;

public class BackupProcessorGroupFile : IBackupProcessor
{
    private readonly IGroupCompressor _groupCompressor;
    private readonly int _maxFilesPerGroup;
    private readonly long _maxBytesPerGroup;
    private readonly Action<string>? _report;

    public BackupProcessorGroupFile(IGroupCompressor groupCompressor, int maxFilesPerGroup = 100, long maxBytesPerGroup = long.MaxValue, Action<string>? report = null)
    {
        _groupCompressor = groupCompressor;
        _maxFilesPerGroup = maxFilesPerGroup;
        _maxBytesPerGroup = maxBytesPerGroup;
        _report = report;
    }

    public void ProcessFiles(IEnumerable<string> files, string sourceRoot, string destinationFolder, List<FileEntry> manifestOut)
    {
        var fileList = files.ToList();
        var groups = new List<List<string>>();
        var currentGroup = new List<string>();
        long currentSize = 0;

        foreach (var file in fileList)
        {
            long fileSize = new FileInfo(file).Length;

            // Start new group if limits are exceeded
            if (currentGroup.Count >= _maxFilesPerGroup || currentSize + fileSize > _maxBytesPerGroup)
            {
                groups.Add(currentGroup);
                currentGroup = new List<string>();
                currentSize = 0;
            }

            currentGroup.Add(file);
            currentSize += fileSize;
        }

        // Add last group if any
        if (currentGroup.Count > 0)
            groups.Add(currentGroup);

        int groupCount = groups.Count;

        for (int i = 0; i < groupCount; i++)
        {
            var groupFiles = groups[i];
            string groupName = $"group_{i + 1:0000}.zip";
            string groupOutputPath = Path.Combine(destinationFolder, groupName);

            _report?.Invoke($"Compressing group {i + 1}/{groupCount} to {groupName} ({groupFiles.Count} files)");

            var groupResult = _groupCompressor.CompressFiles(groupFiles, groupOutputPath, sourceRoot);

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
        }
    }
}