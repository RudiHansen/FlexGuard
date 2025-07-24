namespace FlexGuard.Core.Registry
{
    public class BackupRegistry
    {
        public required string JobName { get; set; }
        public List<BackupEntry> Backups { get; set; } = [];

        public class BackupEntry
        {
            public required DateTime TimestampStart { get; set; }
            public DateTime? TimestampEnd { get; set; }
            public required string Type { get; set; }
            public string ManifestFileName => $"manifest_{TimestampStart:yyyy-MM-ddTHHmm}.json";

            public string DestinationFolderName => $"{TimestampStart:yyyy-MM-ddTHHmm}_{Type}";
        }
    }
}
