namespace FlexGuard.Core.GroupCompression;

public class GroupCompressedFile
{
    public required string SourcePath { get; set; }
    public required string RelativePath { get; set; }
    public required string Hash { get; set; }
}
