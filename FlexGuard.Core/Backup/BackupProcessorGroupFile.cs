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
        int groupCount = (int)Math.Ceiling(totalFiles / (double)_maxFilesPerGroup);

        for (int i = 0; i < groupCount; i++)
        {
            var groupFiles = fileList
                .Skip(i * _maxFilesPerGroup)
                .Take(_maxFilesPerGroup)
                .ToList();

            string groupName = $"group_{i + 1:0000}.zip";
            string groupOutputPath = Path.Combine(destinationFolder, groupName);

            _report?.Invoke($"Compressing group {i + 1}/{groupCount} to {groupName} ({groupFiles.Count} files)");

            var groupResult = _groupCompressor.CompressFiles(groupFiles, groupOutputPath, sourceRoot, (file) =>
            {
                current++;
                _reportProgress?.Invoke(current, totalFiles, file); // FIX: use field, not method parameter
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
        }
    }
}