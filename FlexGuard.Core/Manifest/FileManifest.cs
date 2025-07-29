using FlexGuard.Core.Compression;

namespace FlexGuard.Core.Manifest;

public class FileManifest
{
    public required string JobName { get; set; }
    public required string Type { get; set; } = "Full";
    public required DateTime Timestamp { get; set; }
    public CompressionMethod Compression { get; set; }

    public List<FileEntry> Files { get; set; } = new();
}

public class FileEntry
{
    public required string RelativePath { get; set; }
    public required string ChunkFile { get; set; }
    public required long FileSize { get; set; }
    public required DateTime LastWriteTimeUtc { get; set; }

    public required string Hash { get; set; }
    public bool CompressionSkipped { get; set; } = false;
    public double? CompressionRatio { get; set; } = null;
}