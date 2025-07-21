namespace FlexGuard.Core.Manifest;

public class BackupManifest
{
    public string Type { get; set; } = "Full";
    public DateTime Timestamp { get; set; }
    public List<FileEntry> Files { get; set; } = new();
}
public class FileEntry
{
    // Required for all functionality
    public required string SourcePath { get; set; }
    public required string RelativePath { get; set; }
    public required string Hash { get; set; }
    public required string CompressedFileName { get; set; }
    public required long FileSize { get; set; }
    public required DateTime LastWriteTimeUtc { get; set; }

    // For future logic
    public bool CompressionSkipped { get; set; } = false;
    public bool IsChunked { get; set; } = false;
    public int ChunkCount { get; set; } = 0;
}