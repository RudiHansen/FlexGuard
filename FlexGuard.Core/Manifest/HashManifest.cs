namespace FlexGuard.Core.Manifest;

public class HashManifest
{
    public required string JobName { get; set; }
    public required DateTime Timestamp { get; set; }
    public required string HashAlgorithm { get; set; } = "SHA256";

    public List<ChunkHashEntry> Chunks { get; set; } = new();
}

public class ChunkHashEntry
{
    public required string ChunkFile { get; set; }
    public required string Hash { get; set; }
    public long SizeBytes { get; set; }
}