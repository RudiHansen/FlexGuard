namespace FlexGuard.Core.Manifest;

public class FileEntry
{
    public string SourcePath { get; set; }
    public string RelativePath { get; set; }
    public string Hash { get; set; }
    public string CompressedFileName { get; set; }
}