namespace FlexGuard.Core.Manifest;

public class PendingFileEntry
{
    public required string SourcePath { get; set; }
    public required string RelativePath { get; set; }
    public required long FileSize { get; set; }
    public required DateTime LastWriteTimeUtc { get; set; }
    public required FileGroupType GroupType { get; set; }
}

public enum FileGroupType
{
    Compressible,
    NonCompressible,
    HugeCompressible,
    HugeNonCompressible
}