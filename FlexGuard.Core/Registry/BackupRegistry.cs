using FlexGuard.Core.Options;

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
            public required OperationMode Type { get; set; }
            public string ManifestFileName => $"filemanifest_{TimestampStart:yyyy-MM-ddTHHmm}.json";
            public string HashManifestFileName => $"hashmanifest_{TimestampStart:yyyy-MM-ddTHHmm}.json";

            public string DestinationFolderName => $"{TimestampStart:yyyy-MM-ddTHHmm}_{GetShortType()}";
            private string GetShortType() => Type switch
            {
                OperationMode.FullBackup => "FULL",
                OperationMode.DifferentialBackup => "DIFF",
                OperationMode.Restore => "RESTORE", // Will proberly not be used, but added for completeness
                _ => Type.ToString().ToUpperInvariant()
            };
        }
    }
}
