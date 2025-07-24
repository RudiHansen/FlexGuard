namespace FlexGuard.Core.Manifest;

public class FileGroup
{
    public required int Index { get; set; }
    public required List<PendingFileEntry> Files { get; set; } = new();
    // TODO: Make GroupType required when removal of old code is complete
    public FileGroupType GroupType { get; set; } = FileGroupType.Default;
    //public required FileGroupType GroupType { get; set; }
    public long TotalSize => Files.Sum(f => f.FileSize);
}