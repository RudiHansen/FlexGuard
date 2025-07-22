namespace FlexGuard.Core.Model;

public class FileGroup
{
    public required int Index { get; set; }
    public required List<PendingFileEntry> Files { get; set; } = new();
    public long TotalSize => Files.Sum(f => f.FileSize);
}