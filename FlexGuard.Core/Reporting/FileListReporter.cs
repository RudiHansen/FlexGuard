using FlexGuard.Core.Model;
using FlexGuard.Core.Reporting;

namespace FlexGuard.Core.Reporting;

public static class FileListReporter
{
    public static void ReportSummary(List<PendingFileEntry> files, IMessageReporter reporter)
    {
        // Total count and size
        var totalCount = files.Count;
        var totalSize = files.Sum(f => f.FileSize);

        reporter.Info($"Total files: {totalCount:N0}");
        reporter.Info($"Total size : {FormatBytes(totalSize)}");

        // Grouped by FileGroupType
        var groups = files
            .GroupBy(f => f.GroupType)
            .OrderBy(g => g.Key.ToString());

        foreach (var group in groups)
        {
            var count = group.Count();
            var size = group.Sum(f => f.FileSize);
            reporter.Info($"  {group.Key}: {count:N0} files, {FormatBytes(size)}");
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}