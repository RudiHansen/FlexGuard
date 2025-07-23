using FlexGuard.Core.Config;
using FlexGuard.Core.Model;
using FlexGuard.Core.Reporting;

namespace FlexGuard.Core.Util;

public static class FileCollector
{
    public static List<PendingFileEntry> CollectFiles(
    BackupJobConfig config,
    IMessageReporter reporter,
    DateTime? lastBackupTime = null)
    {
        var entries = new List<PendingFileEntry>();

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
        var extension = Path.GetExtension(path).ToLowerInvariant();

        // Step 0: Extremely small files are not worth compressing
        if (size <= 100)
            return FileGroupType.SmallNonCompressible;

        // Step 1: Check if the file extension is typically already compressed
        if (IsNonCompressibleExtension(extension))
        {
            if (size > 1_000_000_000)
                return FileGroupType.HugeNonCompressible;
            if (size > 100_000_000)
                return FileGroupType.LargeNonCompressible;
            return FileGroupType.SmallNonCompressible;
        }

        // Step 2: Otherwise, classify based on size
        if (size > 1_000_000_000)
            return FileGroupType.HugeCompressible;

        if (size > 100_000_000)
            return FileGroupType.LargeCompressible;

        if (size < 100_000)
            return FileGroupType.SmallCompressible;

        // Step 3: Default fallback for mid-sized files
        return FileGroupType.Default;
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