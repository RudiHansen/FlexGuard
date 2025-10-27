using NUlid;

namespace FlexGuard.Core.Models
{
    /// <summary>
    /// Represents one produced chunk/archive within a backup run.
    /// </summary>
    public sealed class FlexBackupChunkEntry
    {
        // ULID primary key
        public string ChunkEntryId { get; init; } = Ulid.NewUlid().ToString();
        // FK to FlexBackupEntry (also ULID)
        public required string BackupEntryId { get; init; }
        public CompressionMethod CompressionMethod { get; init; } = CompressionMethod.None;
        public RunStatus Status { get; init; } = RunStatus.Running;
        public string? StatusMessage { get; init; }
        // Timing (UTC)
        public required DateTimeOffset StartDateTimeUtc { get; init; }
        public DateTimeOffset? EndDateTimeUtc { get; init; }
        /// <summary>Total runtime for the chunk in ms.</summary>
        public long RunTimeMs { get; init; }
        /// <summary>Time spent creating chunk, ms.</summary>
        public long CreateTimeMs { get; init; }
        /// <summary>Time spent compressing chunk, ms.</summary>
        public long CompressTimeMs { get; init; }
        // Chunk metadata
        public required string ChunkFileName { get; init; }
        public required string ChunkHash { get; init; }
        public long FileSize { get; init; }
        public long FileSizeCompressed { get; init; }
        /// <summary>CPU time consumed for this chunk, in ms.</summary>
        public long CpuTimeMs { get; init; }
        /// <summary>Average CPU usage (%) during work, e.g. 73.2.</summary>
        public double CpuPercent { get; init; }
        public long MemoryStart { get; init; }
        public long MemoryEnd { get; init; }
    }
}