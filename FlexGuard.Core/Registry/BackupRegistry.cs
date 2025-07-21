namespace FlexGuard.Core.Registry
{
    public class BackupRegistry
    {
        public required string JobName { get; set; }
        public List<BackupEntry> Backups { get; set; } = [];

        public class BackupEntry
        {
            public required DateTime Timestamp { get; set; }
            public required string Type { get; set; }
            public required string ManifestFileName { get; set; }
        }
    }
}
