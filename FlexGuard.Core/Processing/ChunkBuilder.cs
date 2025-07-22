using FlexGuard.Core.Options;
using FlexGuard.Core.Model;

namespace FlexGuard.Core.Processing;

public static class ChunkBuilder
{
    public static List<FileGroup> BuildGroups(List<PendingFileEntry> files, ProgramOptions options)
    {
        var groups = new List<FileGroup>();
        var currentGroup = new FileGroup { Index = 0, Files = new List<PendingFileEntry>() };
        long currentSize = 0;

        foreach (var file in files)
        {
            if (currentGroup.Files.Count >= options.MaxFilesPerGroup || currentSize + file.FileSize > options.MaxBytesPerGroup)
            {
                groups.Add(currentGroup);
                currentGroup = new FileGroup { Index = currentGroup.Index + 1, Files = new List<PendingFileEntry>() };
                currentSize = 0;
            }

            currentGroup.Files.Add(file);
            currentSize += file.FileSize;
        }

        if (currentGroup.Files.Count > 0)
        {
            groups.Add(currentGroup);
        }

        return groups;
    }
}
