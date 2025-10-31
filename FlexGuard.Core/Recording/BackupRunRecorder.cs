using FlexGuard.Core.Abstractions;
using FlexGuard.Core.Compression;
using FlexGuard.Core.Models;
using FlexGuard.Core.Options;
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
        private DateTimeOffset _runStartUtc;

        // Totals accumulated across the entire run
        private long _totalFiles;
        private long _totalChunks;
        private long _totalChunkBytes;
        private long _totalChunkCompressedBytes;
        private long _totalFileBytes;
        private long _totalFileCompressedBytes;

        // Per-chunk tracking (optional cache for later completion)
        // We keep a small in-memory structure so that when we complete a chunk,
        // we know its timings/bytes, etc.
        private readonly ConcurrentDictionary<string, ChunkScratchInfo> _chunkScratch
            = new ConcurrentDictionary<string, ChunkScratchInfo>();

        private class ChunkScratchInfo
        {
            public DateTimeOffset StartUtc { get; set; }
            public string ChunkFileName { get; set; } = string.Empty;
            public CompressionMethod CompressionMethod { get; set; }

            // Running totals for this chunk (accumulated via RecordFile)
            public long FileCount;
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
        public async Task<string> StartRunAsync(string jobName,OperationMode mode,CompressionMethod compressionMethod,CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _runActiveFlag, 1) == 1)
                throw new InvalidOperationException("A backup run is already active in this process.");

            _runStartUtc = DateTimeOffset.UtcNow;
            _totalFiles = 0;
            _totalChunks = 0;
            _totalChunkBytes = 0;
            _totalChunkCompressedBytes = 0;
            _totalFileBytes = 0;
            _totalFileCompressedBytes = 0;

            _chunkScratch.Clear();

            // Create initial FlexBackupEntry row with basic info.
            var entry = new FlexBackupEntry
            {
                JobName = jobName,
                OperationMode = mode,
                CompressionMethod = compressionMethod,
                StartDateTimeUtc = _runStartUtc,
                Status = RunStatus.Running,
                StatusMessage = "Running"
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
        public async Task CompleteRunAsync(RunStatus status = RunStatus.Completed,string? errorMessage = "Completed",CancellationToken cancellationToken = default)
        {
            EnsureRunActive();

            DateTimeOffset endUtc = DateTimeOffset.UtcNow;
            var runTime = endUtc - _runStartUtc;
            if (runTime < TimeSpan.Zero) runTime = TimeSpan.Zero;

            // Consistency check: total bytes in chunks should match total bytes in files.
            if (_totalChunkBytes != _totalFileBytes)
            {
                //TODO: Skal nok lige se på hvorfor disse to tal kan afvige lidt.
            }
            long totalBytes = Math.Max(_totalChunkBytes,_totalFileBytes);
            long totalCompressed = Math.Min(_totalChunkCompressedBytes,_totalFileCompressedBytes); // Get the smaller of the two compressed totals.

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
            EnsureRunActive();

            DateTimeOffset startUtc = DateTimeOffset.UtcNow;

            Interlocked.Increment(ref _totalChunks);

            var chunkRow = new FlexBackupChunkEntry
            {
                ChunkIdx = chunkIndex,
                ChunkFileName = chunkFileName,
                BackupEntryId = _currentBackupEntryId!,
                CompressionMethod = compressionMethod,
                StartDateTimeUtc = startUtc,
            };

            await _chunkEntryStore.InsertAsync(chunkRow, cancellationToken).ConfigureAwait(false);

            // Track scratch info for accumulation and later completion.
            // TODO: I am still not sure that I need _chunkScratch, but will deside later. (Remember to clean it up on CompleteChunkAsync)
            _chunkScratch[chunkRow.ChunkEntryId] = new ChunkScratchInfo
            {
                StartUtc = startUtc,
                ChunkFileName = chunkFileName,
                CompressionMethod = compressionMethod,
            };

            return chunkRow.ChunkEntryId;
        }

        /// <summary>
        /// Mark a chunk as completed. Updates the FlexBackupChunkEntry row with final data.
        /// </summary>
        public async Task CompleteChunkAsync(string chunkEntryId, string chunkHash, long finalUnCompressedSizeBytes, long finalCompressedSizeBytes, TimeSpan createTime, TimeSpan compressTime, CancellationToken cancellationToken = default)
        {
            EnsureRunActive();

            DateTimeOffset endUtc = DateTimeOffset.UtcNow;

            if (!_chunkScratch.TryGetValue(chunkEntryId, out var scratch))
            {
                throw new InvalidOperationException(
                    $"No scratch state found for chunk '{chunkEntryId}'. " +
                    "CompleteChunkAsync was called without a matching StartChunkAsync / RecordFileAsync."
                );
            }

            var runTime = endUtc - scratch.StartUtc;
            if (runTime < TimeSpan.Zero) runTime = TimeSpan.Zero;

            // Get existing chunk row (we need to update it)
            var currentChunk = await _chunkEntryStore.GetByIdAsync(chunkEntryId, cancellationToken).ConfigureAwait(false) 
                ?? throw new InvalidOperationException($"No chunk entry found with ChunkEntryId '{chunkEntryId}'.");

            currentChunk.EndDateTimeUtc = endUtc;
            currentChunk.RunTimeMs = (long)runTime.TotalMilliseconds;
            currentChunk.CreateTimeMs = (long)createTime.TotalMilliseconds;
            currentChunk.CompressTimeMs = (long)compressTime.TotalMilliseconds;

            currentChunk.ChunkHash = chunkHash;

            currentChunk.FileSize = finalUnCompressedSizeBytes;
            currentChunk.FileSizeCompressed = finalCompressedSizeBytes;
            currentChunk.FileCount = scratch.FileCount;

            currentChunk.CpuTimeMs = 0;
            currentChunk.CpuPercent = 0;
            currentChunk.MemoryStart = 0;
            currentChunk.MemoryEnd = 0;

            // Persist the final state. Your store should support Update.
            await _chunkEntryStore.UpdateAsync(currentChunk, cancellationToken).ConfigureAwait(false);

            // Clean up scratch for this chunk
            _chunkScratch.TryRemove(chunkEntryId, out _);

            // Update global totals
            Interlocked.Add(ref _totalChunkBytes, currentChunk.FileSize);
            Interlocked.Add(ref _totalChunkCompressedBytes, currentChunk.FileSizeCompressed);
        }

        /// <summary>
        /// Record that we processed a file into a given chunk.
        /// Creates a FlexBackupFileEntry row and updates in-memory totals.
        /// </summary>
        public async Task RecordFileAsync(string chunkEntryId,string relativePath,string fileHash,long originalFileSizeBytes,long compressedFileSizeBytes, DateTimeOffset lastWriteTimeUtc, DateTimeOffset fileProcessStartUtc, DateTimeOffset fileProcessEndUtc,CancellationToken cancellationToken = default)
        {
            EnsureRunActive();

            // Update global totals
            Interlocked.Increment(ref _totalFiles);
            Interlocked.Add(ref _totalFileBytes, originalFileSizeBytes);
            Interlocked.Add(ref _totalFileCompressedBytes, compressedFileSizeBytes);

            // Update chunk scratch (must exist for this chunk)
            if (!_chunkScratch.TryGetValue(chunkEntryId, out var scratch))
            {
                throw new InvalidOperationException(
                    $"RecordFileAsync called for chunk '{chunkEntryId}', but no active chunk state was found. " +
                    "Did you forget to call StartChunkAsync before recording files?"
                );
            }

            Interlocked.Increment(ref scratch.FileCount);

            var runTime = fileProcessEndUtc - fileProcessStartUtc;
            if (runTime < TimeSpan.Zero) runTime = TimeSpan.Zero;

            var fileRow = new FlexBackupFileEntry
            {
                BackupEntryId = _currentBackupEntryId!,
                ChunkEntryId = chunkEntryId,

                StartDateTimeUtc = fileProcessStartUtc,
                EndDateTimeUtc = fileProcessEndUtc,
                RunTimeMs = (long)runTime.TotalMilliseconds,

                // If you will later split "CreateTime" vs "CompressTime", you can set them here.
                CreateTimeMs = 0,
                CompressTimeMs = 0,

                RelativePath = relativePath,
                LastWriteTimeUtc = lastWriteTimeUtc,
                FileHash = fileHash,
                FileSize = originalFileSizeBytes,
                FileSizeCompressed = compressedFileSizeBytes,

                // Performance / telemetry placeholders
                CpuTimeMs = 0,
                CpuPercent = 0,
                MemoryStart = 0,
                MemoryEnd = 0,

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