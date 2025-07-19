namespace FlexGuard.Core.Manifest;

public class FileEntry
{
    public required string SourcePath { get; set; }
    public required string RelativePath { get; set; }
    public required string Hash { get; set; }
    public required string CompressedFileName { get; set; }
}