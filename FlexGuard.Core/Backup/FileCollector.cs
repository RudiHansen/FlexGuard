using FlexGuard.Core.Config;
using FlexGuard.Core.Manifest;
using FlexGuard.Core.Reporting;

namespace FlexGuard.Core.Backup;

public static class FileCollector
{
    public static List<PendingFileEntry> CollectFiles(
    BackupJobConfig config,
    IMessageReporter reporter,
    DateTime? lastBackupTime = null)
    {
        var entries = new List<PendingFileEntry>();
        // Collect files from all sources
        foreach (var source in config.Sources)
        {
            var files = FileEnumerator.GetFiles(source.Path, source.Exclude, reporter);

            foreach (var file in files)
            {
                try
                {
                    var info = new FileInfo(file);

                    if (lastBackupTime == null || info.LastWriteTimeUtc > lastBackupTime.Value)
                    {
                        var fullPath = Path.GetFullPath(file);
                        var drive = Path.GetPathRoot(fullPath)?.TrimEnd('\\', '/').Replace(":", "") ?? "UNKNOWN";
                        var relativeToRoot = Path.GetRelativePath(Path.GetPathRoot(fullPath) ?? "", fullPath);
                        var relativePath = Path.Combine(drive, relativeToRoot).Replace('\\', '/');

                        var groupType = DetermineGroupType(file, info.Length);

                        entries.Add(new PendingFileEntry
                        {
                            SourcePath = fullPath,
                            RelativePath = relativePath,
                            FileSize = info.Length,
                            LastWriteTimeUtc = info.LastWriteTimeUtc,
                            GroupType = groupType
                        });
                    }
                }
                catch (Exception ex)
                {
                    reporter.Warning($"Could not access file: {file} - {ex.Message}");
                }
            }
        }
        return entries;
    }

    private static FileGroupType DetermineGroupType(string path, long size)
    {
        const long HugeFileThreshold = 1_000_000_000;
        var extension = Path.GetExtension(path)?.ToLowerInvariant() ?? "";

        // Step 1: Check if the file extension is typically already compressed
        if (IsNonCompressibleExtension(extension))
        {
            if (size > HugeFileThreshold)
                return FileGroupType.HugeNonCompressible;
            return FileGroupType.NonCompressible;
        }

        // Step 2: Otherwise, classify based on size
        if (size > HugeFileThreshold)
            return FileGroupType.HugeCompressible;

        // Step 3: Default fallback for mid-sized files
        return FileGroupType.Compressible;
    }

    private static readonly HashSet<string> NonCompressibleExtensions = new(StringComparer.OrdinalIgnoreCase)
{
    ".zip", ".rar", ".7z", ".mp3", ".mp4", ".mkv", ".avi", ".gz"
    //".jpg", ".jpeg", ".png", ".gif",".pdf"
};

    private static bool IsNonCompressibleExtension(string ext)
    {
        return NonCompressibleExtensions.Contains(ext);
    }
}