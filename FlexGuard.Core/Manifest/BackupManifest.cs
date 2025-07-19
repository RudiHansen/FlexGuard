namespace FlexGuard.Core.Manifest;

public class BackupManifest
{
    public string Type { get; set; } = "Full";
    public DateTime Timestamp { get; set; }
    public List<FileEntry> Files { get; set; } = new();
}