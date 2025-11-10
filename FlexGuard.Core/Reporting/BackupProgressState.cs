using System;
using System.Threading;

namespace FlexGuard.Core.Reporting
{
    /// <summary>
    /// Thread-safe state tracker for backup progress.
    /// Tracks processed files, bytes, chunks, speed and ETA.
    /// </summary>
    public sealed class BackupProgressState
    {
        private long _processedBytes;
        private int _processedFiles;
        private int _completedChunks;

        private long _bytesAtLastUpdate;
        private DateTimeOffset _lastUpdateUtc;

        public long TotalBytes { get; init; }
        public int TotalFiles { get; init; }
        public int TotalChunks { get; init; }

        public DateTimeOffset StartTimeUtc { get; } = DateTimeOffset.UtcNow;

        public BackupProgressState()
        {
            _lastUpdateUtc = StartTimeUtc;
        }

        /// <summary>
        /// Adds a processed file (bytes and file count).
        /// </summary>
        public void AddFile(long fileBytes)
        {
            Interlocked.Add(ref _processedBytes, fileBytes);
            Interlocked.Increment(ref _processedFiles);
        }

        /// <summary>
        /// Marks a chunk as completed.
        /// </summary>
        public void CompleteChunk()
        {
            Interlocked.Increment(ref _completedChunks);
        }

        /// <summary>
        /// Returns a snapshot of current progress metrics.
        /// </summary>
        public ProgressSnapshot Snapshot()
        {
            var now = DateTimeOffset.UtcNow;
            var elapsed = now - StartTimeUtc;

            long processedBytes = Interlocked.Read(ref _processedBytes);
            int processedFiles = Volatile.Read(ref _processedFiles);
            int completedChunks = Volatile.Read(ref _completedChunks);

            double totalMB = TotalBytes / 1_000_000.0;
            double processedMB = processedBytes / 1_000_000.0;

            double progressPercent = TotalBytes > 0
                ? (processedBytes / (double)TotalBytes) * 100.0
                : 0.0;

            // Calculate speed and ETA
            double deltaSec = Math.Max(1, (now - _lastUpdateUtc).TotalSeconds);
            double bytesSinceLast = processedBytes - Interlocked.Read(ref _bytesAtLastUpdate);
            double speedMBs = bytesSinceLast / 1_000_000.0 / deltaSec;

            // Update internal speed sample reference
            _lastUpdateUtc = now;
            Interlocked.Exchange(ref _bytesAtLastUpdate, processedBytes);

            TimeSpan eta = TimeSpan.Zero;
            if (speedMBs > 0 && processedBytes > 0 && processedBytes < TotalBytes)
            {
                double remainingMB = (TotalBytes - processedBytes) / 1_000_000.0;
                eta = TimeSpan.FromSeconds(remainingMB / speedMBs);
            }

            return new ProgressSnapshot(
                processedFiles,
                TotalFiles,
                processedMB,
                totalMB,
                completedChunks,
                TotalChunks,
                progressPercent,
                speedMBs,
                elapsed,
                eta);
        }

        /// <summary>
        /// Immutable snapshot struct for displaying progress.
        /// </summary>
        public readonly struct ProgressSnapshot
        {
            public int ProcessedFiles { get; }
            public int TotalFiles { get; }
            public double ProcessedMB { get; }
            public double TotalMB { get; }
            public int CompletedChunks { get; }
            public int TotalChunks { get; }
            public double ProgressPercent { get; }
            public double SpeedMBs { get; }
            public TimeSpan Elapsed { get; }
            public TimeSpan ETA { get; }

            public ProgressSnapshot(
                int processedFiles, int totalFiles,
                double processedMB, double totalMB,
                int completedChunks, int totalChunks,
                double progressPercent, double speedMBs,
                TimeSpan elapsed, TimeSpan eta)
            {
                ProcessedFiles = processedFiles;
                TotalFiles = totalFiles;
                ProcessedMB = processedMB;
                TotalMB = totalMB;
                CompletedChunks = completedChunks;
                TotalChunks = totalChunks;
                ProgressPercent = progressPercent;
                SpeedMBs = speedMBs;
                Elapsed = elapsed;
                ETA = eta;
            }
        }
    }
}