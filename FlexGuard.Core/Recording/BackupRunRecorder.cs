using NUlid;
using FlexGuard.Core.Abstractions;
using FlexGuard.Core.Models;
using System.Collections.Concurrent;

namespace FlexGuard.Core.Recording
{
    /// <summary>
    /// BackupRunRecorder is responsible for recording all observable facts about a backup run:
    /// - Run start / run completion (FlexBackupEntry)
    /// - Chunk start / chunk completion (FlexBackupChunkEntry)
    /// - File processed (FlexBackupFileEntry)
    ///
    /// It also keeps running totals (files, bytes, etc.) so CompleteRun() can finalize summary data.
    /// 
    /// Assumptions:
    /// - Only one backup run is active at a time in this process.
    /// - Methods may be called from multiple worker threads (future parallelization),
    ///   so internal updates should be thread-safe.
    /// </summary>
    public class BackupRunRecorder
    {
        // --- Dependencies (injected) ---
        private readonly IFlexBackupEntryStore _backupEntryStore;
        private readonly IFlexBackupChunkEntryStore _chunkEntryStore;
        private readonly IFlexBackupFileEntryStore _fileEntryStore;

        // --- Current run state ---
        private string? _currentBackupEntryId;
        private FlexBackupEntry? _currentBackupEntry;
        private DateTime _runStartUtc;

        // Totals accumulated across the entire run
        private long _totalFiles;
        private long _totalChunks;
        private long _totalUncompressedBytes;
        private long _totalCompressedBytes;

        // Per-chunk tracking (optional cache for later completion)
        // We keep a small in-memory structure so that when we complete a chunk,
        // we know its timings/bytes, etc.
        private readonly ConcurrentDictionary<string, ChunkScratchInfo> _chunkScratch
            = new ConcurrentDictionary<string, ChunkScratchInfo>();

        private class ChunkScratchInfo
        {
            public DateTime StartUtc { get; set; }
            public string ChunkFileName { get; set; } = string.Empty;
            public CompressionMethod CompressionMethod { get; set; }

            // Running totals for this chunk (accumulated via RecordFile)
            public long FileCount;
            public long UncompressedBytes;
            public long CompressedBytes;
        }

        // Thread-safety helpers
        private int _runActiveFlag = 0; // 0 = no run, 1 = active

        public BackupRunRecorder(IFlexBackupEntryStore backupEntryStore,IFlexBackupChunkEntryStore chunkEntryStore,IFlexBackupFileEntryStore fileEntryStore)
        {
            _backupEntryStore = backupEntryStore ?? throw new ArgumentNullException(nameof(backupEntryStore));
            _chunkEntryStore = chunkEntryStore ?? throw new ArgumentNullException(nameof(chunkEntryStore));
            _fileEntryStore = fileEntryStore ?? throw new ArgumentNullException(nameof(fileEntryStore));
        }

        /// <summary>
        /// Start a new backup run.
        /// Creates a FlexBackupEntry row and initializes in-memory totals.
        /// Assumes there is no active run.
        /// </summary>
        public async Task<string> StartRunAsync(string jobName,OperationMode mode,CompressionMethod compressionMethod,string hashAlgorithm = "SHA256",CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _runActiveFlag, 1) == 1)
                throw new InvalidOperationException("A backup run is already active in this process.");

            _runStartUtc = DateTime.UtcNow;
            _totalFiles = 0;
            _totalChunks = 0;
            _totalUncompressedBytes = 0;
            _totalCompressedBytes = 0;

            _chunkScratch.Clear();

            // Create initial FlexBackupEntry row with basic info.
            var entry = new FlexBackupEntry
            {
                JobName = jobName,
                OperationMode = mode,
                CompressionMethod = compressionMethod,
                StartDateTimeUtc = _runStartUtc,
            };
            _currentBackupEntryId = entry.BackupEntryId;

            await _backupEntryStore.InsertAsync(entry, cancellationToken).ConfigureAwait(false);
            _currentBackupEntry = entry;

            return _currentBackupEntryId;
        }

        /// <summary>
        /// Complete the run: finalize FlexBackupEntry with end time and totals.
        /// You can also pass status / error info here if you add those columns.
        /// </summary>
        public async Task CompleteRunAsync(RunStatus status = RunStatus.Completed,string? errorMessage = null,CancellationToken cancellationToken = default)
        {
            EnsureRunActive();

            var endUtc = DateTime.UtcNow;
            var runTime = endUtc - _runStartUtc;
            if (runTime < TimeSpan.Zero) runTime = TimeSpan.Zero;

            var totalBytes = _totalUncompressedBytes;
            var totalCompressed = _totalCompressedBytes;
            double ratio = 0;
            if (totalBytes > 0)
            {
                ratio = (double)totalCompressed / totalBytes;
            }

            if (_currentBackupEntry == null)
            {
                throw new InvalidOperationException("No active backup run. CompleteRunAsync() was called without a matching StartRunAsync().");
            }

            _currentBackupEntry.EndDateTimeUtc = endUtc;
            _currentBackupEntry.RunTimeMs = (long)runTime.TotalMilliseconds;

            _currentBackupEntry.Status = status;
            _currentBackupEntry.StatusMessage = errorMessage;

            _currentBackupEntry.TotalFiles = _totalFiles;
            _currentBackupEntry.TotalChunks = _totalChunks;
            _currentBackupEntry.TotalBytes = totalBytes;
            _currentBackupEntry.TotalBytesCompressed = totalCompressed;
            _currentBackupEntry.CompressionRatio = ratio;

            await _backupEntryStore.UpdateAsync(_currentBackupEntry, cancellationToken).ConfigureAwait(false);

            // Mark run inactive
            _currentBackupEntryId = null;
            _currentBackupEntry = null;
            Interlocked.Exchange(ref _runActiveFlag, 0);

            // Clear scratch just in case
            _chunkScratch.Clear();
        }

        /// <summary>
        /// Register that we are starting to build a new chunk.
        /// Returns a ChunkEntryId that will be used in later calls.
        /// </summary>
        public async Task<string> StartChunkAsync(string chunkFileName,CompressionMethod compressionMethod,int chunkIndex,CancellationToken cancellationToken = default)
        {
            //TODO: Metoden er ikke færdig endnu, skal gennemgås før den bruges.
            EnsureRunActive();

            var chunkEntryId = Ulid.NewUlid().ToString();
            var startUtc = DateTime.UtcNow;

            // Track scratch info for accumulation and later completion.
            _chunkScratch[chunkEntryId] = new ChunkScratchInfo
            {
                StartUtc = startUtc,
                ChunkFileName = chunkFileName,
                CompressionMethod = compressionMethod,
                FileCount = 0,
                UncompressedBytes = 0,
                CompressedBytes = 0
            };

            Interlocked.Increment(ref _totalChunks);

            var chunkRow = new FlexBackupChunkEntry
            {
                ChunkEntryId = chunkEntryId,
                BackupEntryId = _currentBackupEntryId!,
                CompressionMethod = compressionMethod,

                StartDateTimeUtc = startUtc,
                EndDateTimeUtc = null,
                RunTimeMs = 0,
                CreateTimeMs = 0,
                CompressTimeMs = 0,

                ChunkFileName = chunkFileName,
                ChunkHash = "", // will be known at completion
                FileSize = 0,    // will fill in CompleteChunk
                FileSizeCompressed = 0,    // will fill in CompleteChunk

                CpuTimeMs = 0,
                CpuPercent = 0,
                MemoryStart = 0,
                MemoryEnd = 0,

                // If you keep ChunkIndex in DB, add it here:
                // ChunkIndex = chunkIndex,
            };

            await _chunkEntryStore.InsertAsync(chunkRow, cancellationToken).ConfigureAwait(false);

            return chunkEntryId;
        }

        /// <summary>
        /// Mark a chunk as completed. Updates the FlexBackupChunkEntry row with final data.
        /// </summary>
        public async Task CompleteChunkAsync(string chunkEntryId, string chunkHash, long finalCompressedSizeBytes, TimeSpan createTime, TimeSpan compressTime, CancellationToken cancellationToken = default)
        {
            //TODO: Metoden er ikke færdig endnu, skal gennemgås før den bruges.
            EnsureRunActive();

            var endUtc = DateTime.UtcNow;

            if (!_chunkScratch.TryGetValue(chunkEntryId, out var scratch))
            {
                // We didn't have scratch info; fallback to defaults.
                scratch = new ChunkScratchInfo
                {
                    StartUtc = endUtc,
                    ChunkFileName = "",
                    CompressionMethod = CompressionMethod.Zstd,
                    FileCount = 0,
                    UncompressedBytes = 0,
                    CompressedBytes = 0
                };
            }

            var runTime = endUtc - scratch.StartUtc;
            if (runTime < TimeSpan.Zero) runTime = TimeSpan.Zero;

            // Build updated row
            var updatedChunkRow = new FlexBackupChunkEntry
            {
                ChunkEntryId = chunkEntryId,
                BackupEntryId = _currentBackupEntryId!,
                CompressionMethod = scratch.CompressionMethod,

                StartDateTimeUtc = scratch.StartUtc,
                EndDateTimeUtc = endUtc,
                RunTimeMs = runTime.Milliseconds,
                CreateTimeMs = createTime.Milliseconds,
                CompressTimeMs = compressTime.Milliseconds,

                ChunkFileName = scratch.ChunkFileName,
                ChunkHash = chunkHash,

                FileSize = scratch.UncompressedBytes,
                FileSizeCompressed = finalCompressedSizeBytes,

                CpuTimeMs = 0,
                CpuPercent = 0,
                MemoryStart = 0,
                MemoryEnd = 0,

                // If you keep ChunkIndex in DB, you might need to track/store it too.
            };

            // Persist the final state. Your store should support Update.
            await _chunkEntryStore.UpdateAsync(updatedChunkRow, cancellationToken).ConfigureAwait(false);

            // Clean up scratch for this chunk
            _chunkScratch.TryRemove(chunkEntryId, out _);
        }

        /// <summary>
        /// Record that we processed a file into a given chunk.
        /// Creates a FlexBackupFileEntry row and updates in-memory totals.
        /// </summary>
        public async Task RecordFileAsync(string chunkEntryId,string relativePath,string fileHash,long originalFileSizeBytes,long? compressedFileSizeBytes,DateTime lastWriteTimeUtc,DateTime fileProcessStartUtc,DateTime fileProcessEndUtc,CancellationToken cancellationToken = default)
        {
            //TODO: Metoden er ikke færdig endnu, skal gennemgås før den bruges.
            EnsureRunActive();

            var fileEntryId = Ulid.NewUlid().ToString();

            // Update global totals
            Interlocked.Increment(ref _totalFiles);
            Interlocked.Add(ref _totalUncompressedBytes, originalFileSizeBytes);
            if (compressedFileSizeBytes.HasValue)
            {
                Interlocked.Add(ref _totalCompressedBytes, compressedFileSizeBytes.Value);
            }

            // Update chunk scratch
            if (_chunkScratch.TryGetValue(chunkEntryId, out var scratch))
            {
                Interlocked.Increment(ref scratch.FileCount);
                Interlocked.Add(ref scratch.UncompressedBytes, originalFileSizeBytes);
                if (compressedFileSizeBytes.HasValue)
                {
                    Interlocked.Add(ref scratch.CompressedBytes, compressedFileSizeBytes.Value);
                }
            }

            var runTime = fileProcessEndUtc - fileProcessStartUtc;
            if (runTime < TimeSpan.Zero) runTime = TimeSpan.Zero;

            var fileRow = new FlexBackupFileEntry
            {
                FileEntryId = fileEntryId,
                BackupEntryId = _currentBackupEntryId!,
                ChunkEntryId = chunkEntryId,

                StartDateTimeUtc = fileProcessStartUtc,
                EndDateTimeUtc = fileProcessEndUtc,
                RunTimeMs = (long)runTime.TotalMilliseconds,    //TODO: Se lige på hvad vi bør gøre her.

                // If you will later split "CreateTime" vs "CompressTime", you can set them here.
                CreateTimeMs = 0,
                CompressTimeMs = 0,

                RelativePath = relativePath,
                LastWriteTimeUtc = lastWriteTimeUtc,
                FileHash = fileHash,
                FileSize = originalFileSizeBytes,
                FileSizeCompressed = compressedFileSizeBytes ?? 0,

                // Performance / telemetry placeholders
                CpuTimeMs = 0,
                CpuPercent = 0,
                MemoryStart = 0,
                MemoryEnd = 0,

                // Optional: if you add LastWriteTimeUtc, add it here.
                // LastWriteTimeUtc = lastWriteTimeUtc
            };

            await _fileEntryStore.InsertAsync(fileRow, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Helper to guard methods that require an active run.
        /// </summary>
        private void EnsureRunActive()
        {
            if (_runActiveFlag == 0 || _currentBackupEntryId == null)
                throw new InvalidOperationException("No active backup run. Did you call StartRunAsync()?");
        }

        /// <summary>
        /// Expose the currently active BackupEntryId (for diagnostics / logging).
        /// Returns null if no run is active.
        /// </summary>
        public string? GetCurrentBackupEntryId()
        {
            return _currentBackupEntryId;
        }
    }
}