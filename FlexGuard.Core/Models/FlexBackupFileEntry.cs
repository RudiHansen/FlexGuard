using NUlid;

namespace FlexGuard.Core.Models
{
    /// <summary>
    /// Per-file tracking within a backup chunk.
    /// </summary>
    public sealed class FlexBackupFileEntry
    {
        // ULID primary key
        public string FileEntryId { get; init; } = Ulid.NewUlid().ToString();
        // FK to FlexBackupChunkEntry
        public required string ChunkEntryId { get; init; }
        // FK to FlexBackupEntry
        public required string BackupEntryId { get; init; }
        public RunStatus Status { get; init; } = RunStatus.Running;
        public string? StatusMessage { get; init; }
        // Timing
        public required DateTimeOffset StartDateTimeUtc { get; init; }
        public DateTimeOffset? EndDateTimeUtc { get; init; }
        /// <summary>Total runtime for handling this file, in ms.</summary>
        public long RunTimeMs { get; init; }
        /// <summary>Time spent creating archive data for this file, ms.</summary>
        public long CreateTimeMs { get; init; }
        /// <summary>Time spent compressing this file, ms.</summary>
        public long CompressTimeMs { get; init; }
        public required string RelativePath { get; init; }
        public required DateTimeOffset LastWriteTimeUtc { get; init; }
        public required string FileHash { get; init; }
        public long FileSize { get; init; }
        public long FileSizeCompressed { get; init; }
        public long CpuTimeMs { get; init; }
        public double CpuPercent { get; init; }
        public long MemoryStart { get; init; }
        public long MemoryEnd { get; init; }
    }
}
