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
        public RunStatus Status { get; init; } = RunStatus.Running;
        public string? StatusMessage { get; init; }
        // UTC timestamps
        public required DateTimeOffset StartDateTimeUtc { get; init; }
        public DateTimeOffset? EndDateTimeUtc { get; init; }
        /// <summary>Total runtime in milliseconds for the job.</summary>
        public long RunTimeMs { get; init; }
        // Aggregates
        public long TotalFiles { get; init; }
        public long TotalGroups { get; init; }
        public long TotalBytes { get; init; }
        public long TotalBytesCompressed { get; init; }
        /// <summary>
        /// Compression ratio (compressed/original). e.g. 0.42 = 42%.
        /// </summary>
        public double CompressionRatio { get; init; }
    }
}