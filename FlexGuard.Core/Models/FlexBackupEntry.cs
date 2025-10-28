using NUlid;

namespace FlexGuard.Core.Models
{
    /// <summary>
    /// High-level run summary for a backup execution/job.
    /// </summary>
    public sealed class FlexBackupEntry
    {
        // ULID primary key
        public string BackupEntryId { get; init; } = Ulid.NewUlid().ToString();
        public required string JobName { get; init; }
        public OperationMode OperationMode { get; init; } = OperationMode.Unknown;
        public CompressionMethod CompressionMethod { get; init; } = CompressionMethod.None;
        public RunStatus Status { get; set; } = RunStatus.Running;
        public string? StatusMessage { get; set; }
        // UTC timestamps
        public required DateTimeOffset StartDateTimeUtc { get; init; }
        public DateTimeOffset? EndDateTimeUtc { get; set; }
        /// <summary>Total runtime in milliseconds for the job.</summary>
        public long RunTimeMs { get; set; }
        // Aggregates
        public long TotalFiles { get; set; }
        public long TotalChunks { get; set; }
        public long TotalBytes { get; set; }
        public long TotalBytesCompressed { get; set; }
        /// <summary>
        /// Compression ratio (compressed/original). e.g. 0.42 = 42%.
        /// </summary>
        public double CompressionRatio { get; set; }
    }
}