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
                        var relativePath = Path.GetRelativePath(source.Path, file);
                        var groupType = DetermineGroupType(file, info.Length);

                        entries.Add(new PendingFileEntry
                        {
                            SourcePath = file,
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

        // Step 0: Very small files are never worth compressing
        if (size <= 100)
        {
            return FileGroupType.SmallNonCompressible;
        }

        // Known archive/media formats that are typically already compressed
        if (IsNonCompressibleExtension(extension))
        {
            return size < 100_000
                ? FileGroupType.SmallNonCompressible
                : FileGroupType.LargeNonCompressible;
        }

        // Small files that are likely compressible
        if (size < 100_000)
        {
            return FileGroupType.SmallCompressible;
        }

        // Everything else
        return size > 100_000_000
            ? FileGroupType.LargeCompressible
            : FileGroupType.Default;
    }

    private static bool IsNonCompressibleExtension(string ext)
    {
        return ext switch
        {
            ".zip" => true,
            ".rar" => true,
            ".7z" => true,
            //".jpg" => true,
            //".jpeg" => true,
            //".png" => true,
            //".gif" => true,
            ".mp3" => true,
            ".mp4" => true,
            ".mkv" => true,
            ".avi" => true,
            ".gz" => true,
            //".pdf" => true,
            _ => false
        };
    }
}