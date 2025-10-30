using FlexGuard.Core.Compression;
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
        public required int ChunkIdx { get; init; }
        public required string BackupEntryId { get; init; }
        public CompressionMethod CompressionMethod { get; init; } = CompressionMethod.Zstd;
        public RunStatus Status { get; init; } = RunStatus.Running;
        public string? StatusMessage { get; init; }
        // Timing (UTC)
        public required DateTimeOffset StartDateTimeUtc { get; init; }
        public DateTimeOffset? EndDateTimeUtc { get; set; }
        /// <summary>Total runtime for the chunk in ms.</summary>
        public long RunTimeMs { get; set; }
        /// <summary>Time spent creating chunk, ms.</summary>
        public long CreateTimeMs { get; set; }
        /// <summary>Time spent compressing chunk, ms.</summary>
        public long CompressTimeMs { get; set; }
        // Chunk metadata
        public required string ChunkFileName { get; init; }
        public string ChunkHash { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public long FileSizeCompressed { get; set; }
        public long FileCount { get; set; }
        /// <summary>CPU time consumed for this chunk, in ms.</summary>
        public long CpuTimeMs { get; set; }
        /// <summary>Average CPU usage (%) during work, e.g. 73.2.</summary>
        public double CpuPercent { get; set; }
        public long MemoryStart { get; set; }
        public long MemoryEnd { get; set; }
    }
}