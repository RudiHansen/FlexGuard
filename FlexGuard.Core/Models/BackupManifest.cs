namespace FlexGuard.Core.Models
{
    public sealed class BackupManifest
    {
        public int ManifestVersion { get; set; } = 1;
        public DateTimeOffset CreatedOnUtc { get; set; } = DateTimeOffset.UtcNow;
        public string CreatedBy { get; set; } = "FlexGuard";

        public FlexBackupEntry BackupEntry { get; set; } = default!;
        public List<FlexBackupChunkEntry> Chunks { get; set; } = new();
        public List<FlexBackupFileEntry> Files { get; set; } = new();
    }
}