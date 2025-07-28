using FlexGuard.Core.Manifest;
using FlexGuard.Core.Profiling;
using FlexGuard.Core.Reporting;
using System.Xml.Schema;

namespace FlexGuard.Core.Backup;

public static class FileGrouper
{
    public static List<FileGroup> GroupFiles(
        List<PendingFileEntry> files,
        int maxFilesPerGroup,
        long maxBytesPerGroup)
    {
        var result = new List<FileGroup>();
        int groupIndex = 0;

        // Group files by GroupType
        var groupedByType = files.GroupBy(f => f.GroupType);
        using (var scope = PerformanceTracker.Instance.TrackSection("Group Files"))
        {
            foreach (var typeGroup in groupedByType)
            {
                var currentFiles = new List<PendingFileEntry>();
                long currentSize = 0;

                foreach (var file in typeGroup)
                {
                    bool exceedsFileLimit = currentFiles.Count >= maxFilesPerGroup;
                    bool exceedsSizeLimit = currentSize + file.FileSize > maxBytesPerGroup;

                    if ((exceedsFileLimit || exceedsSizeLimit) && currentFiles.Count > 0)
                    {
                        result.Add(new FileGroup
                        {
                            Index = groupIndex++,
                            GroupType = typeGroup.Key,
                            Files = new List<PendingFileEntry>(currentFiles)
                        });

                        currentFiles.Clear();
                        currentSize = 0;
                    }

                    currentFiles.Add(file);
                    currentSize += file.FileSize;
                }

                // Add remaining files in the group
                if (currentFiles.Count > 0)
                {
                    result.Add(new FileGroup
                    {
                        Index = groupIndex++,
                        GroupType = typeGroup.Key,
                        Files = new List<PendingFileEntry>(currentFiles)
                    });
                }
                scope.AddListItem("groups", new
                {
                    groupType = typeGroup.Key.ToString(),
                    fileCount = typeGroup.Count(),
                    totalBytes = typeGroup.Sum(f => f.FileSize)
                });
            }
        }
        return result;
    }
}