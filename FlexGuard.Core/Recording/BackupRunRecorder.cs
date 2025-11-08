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
        private readonly ConcurrentDictionary<string, ChunkScratchInfo> _chunkScratch = new ConcurrentDictionary<string, ChunkScratchInfo>();

        private class ChunkScratchInfo
        {
            public DateTimeOffset StartUtc { get; set; }
            public string ChunkFileName { get; set; } = string.Empty;
            public CompressionMethod CompressionMethod { get; set; }
            public long FileCount;
        }

        // Thread-safety helpers
        private int _runActiveFlag = 0; // 0 = no run, 1 = active
        private bool _isClosing = false;
        private readonly object _stateLock = new();

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
        public async Task<string> StartRunAsync(string jobName,string destinationBackupFolder,OperationMode mode,CompressionMethod compressionMethod,
            long runTimeCollectFilesMs,CancellationToken cancellationToken = default)
        {
            // én aktiv ad gangen i processen
            if (Interlocked.Exchange(ref _runActiveFlag, 1) == 1)
                throw new InvalidOperationException("A backup run is already active in this process.");

            FlexBackupEntry entry;
            lock (_stateLock)
            {
                _isClosing = false;
                _runStartUtc = DateTimeOffset.UtcNow;

                _totalFiles = 0;
                _totalChunks = 0;
                _totalChunkBytes = 0;
                _totalChunkCompressedBytes = 0;
                _totalFileBytes = 0;
                _totalFileCompressedBytes = 0;

                _chunkScratch.Clear();

                entry = new FlexBackupEntry
                {
                    JobName = jobName,
                    DestinationBackupFolder = destinationBackupFolder,
                    OperationMode = mode,
                    CompressionMethod = compressionMethod,
                    StartDateTimeUtc = _runStartUtc,
                    RunTimeCollectFilesMs = runTimeCollectFilesMs,
                    Status = RunStatus.Running,
                    StatusMessage = "Running"
                };

                _currentBackupEntryId = entry.BackupEntryId;
                _currentBackupEntry = entry;
            }

            // DB-call udenfor låsen
            await _backupEntryStore.InsertAsync(entry, cancellationToken);

            return entry.BackupEntryId;
        }

        /// <summary>
        /// Complete the run: finalize FlexBackupEntry with end time and totals.
        /// </summary>
        public async Task CompleteRunAsync(RunStatus status = RunStatus.Completed,string? errorMessage = "Completed",CancellationToken cancellationToken = default)
        {
            FlexBackupEntry entrySnapshot;
            DateTimeOffset endUtc;
            long totalFiles;
            long totalChunks;
            long totalChunkBytes;
            long totalChunkCompressedBytes;
            long totalFileBytes;
            long totalFileCompressedBytes;
            DateTimeOffset runStart;

            lock (_stateLock)
            {
                if (_runActiveFlag == 0 || _currentBackupEntry is null)
                    throw new InvalidOperationException("No active backup run. CompleteRunAsync() was called without a matching StartRunAsync().");

                // vi er nu i lukke-fase
                _isClosing = true;

                // snapshot af værdier
                entrySnapshot = _currentBackupEntry;
                endUtc = DateTimeOffset.UtcNow;
                totalFiles = _totalFiles;
                totalChunks = _totalChunks;
                totalChunkBytes = _totalChunkBytes;
                totalChunkCompressedBytes = _totalChunkCompressedBytes;
                totalFileBytes = _totalFileBytes;
                totalFileCompressedBytes = _totalFileCompressedBytes;
                runStart = _runStartUtc;
            }

            // beregn udenfor lås
            var runTime = endUtc - runStart;
            if (runTime < TimeSpan.Zero) runTime = TimeSpan.Zero;

            long totalBytes = Math.Max(totalChunkBytes, totalFileBytes);
            long totalCompressed = Math.Min(totalChunkCompressedBytes, totalFileCompressedBytes);

            double ratio = 0;
            if (totalBytes > 0)
                ratio = (double)totalCompressed / totalBytes;

            // opdater snapshot
            entrySnapshot.EndDateTimeUtc = endUtc;
            entrySnapshot.RunTimeMs = (long)runTime.TotalMilliseconds;
            entrySnapshot.Status = status;
            entrySnapshot.StatusMessage = errorMessage;
            entrySnapshot.TotalFiles = totalFiles;
            entrySnapshot.TotalChunks = totalChunks;
            entrySnapshot.TotalBytes = totalBytes;
            entrySnapshot.TotalBytesCompressed = totalCompressed;
            entrySnapshot.CompressionRatio = ratio;

            await _backupEntryStore.UpdateAsync(entrySnapshot, cancellationToken);

            // ryd state til sidst
            lock (_stateLock)
            {
                _currentBackupEntryId = null;
                _currentBackupEntry = null;
                _isClosing = false;
                Interlocked.Exchange(ref _runActiveFlag, 0);
                _chunkScratch.Clear();
            }
        }

        /// <summary>
        /// Register that we are starting to build a new chunk.
        /// </summary>
        public async Task<string> StartChunkAsync(string chunkFileName,CompressionMethod compressionMethod,int chunkIndex,CancellationToken cancellationToken = default)
        {
            string backupEntryId;
            DateTimeOffset startUtc;

            lock (_stateLock)
            {
                if (!IsRunActiveUnsafe())
                    return string.Empty; // run lukker, ignorer

                backupEntryId = _currentBackupEntryId!;
                startUtc = DateTimeOffset.UtcNow;
                Interlocked.Increment(ref _totalChunks);
            }

            var chunkRow = new FlexBackupChunkEntry
            {
                ChunkIdx = chunkIndex,
                ChunkFileName = chunkFileName,
                BackupEntryId = backupEntryId,
                CompressionMethod = compressionMethod,
                StartDateTimeUtc = startUtc,
                Status = RunStatus.Running,
                StatusMessage = "Running"
            };

            await _chunkEntryStore.InsertAsync(chunkRow, cancellationToken);

            // scratch kan godt oprettes uden lås, dictionary er concurrent
            _chunkScratch[chunkRow.ChunkEntryId] = new ChunkScratchInfo
            {
                StartUtc = startUtc,
                ChunkFileName = chunkFileName,
                CompressionMethod = compressionMethod,
            };

            return chunkRow.ChunkEntryId;
        }

        /// <summary>
        /// Mark a chunk as completed.
        /// </summary>
        public async Task CompleteChunkAsync(string chunkEntryId,string chunkHash,CompressionMethod compressionMethod,long finalUnCompressedSizeBytes,
            long finalCompressedSizeBytes, TimeSpan createTime, TimeSpan compressTime, TimeSpan cpuTime, double peakCpuPercent,
            long peakWorkingSetBytes, long? peakManagedBytes, CancellationToken cancellationToken = default)
        {
            // hvis run er ved at lukke, så ignorer vi bare stille
            if (!IsRunActive())
                return;

            DateTimeOffset endUtc = DateTimeOffset.UtcNow;

            if (!_chunkScratch.TryGetValue(chunkEntryId, out var scratch))
            {
                throw new InvalidOperationException($"No scratch state found for chunk '{chunkEntryId}'. " +
                    "CompleteChunkAsync was called without a matching StartChunkAsync / RecordFileAsync.");
            }

            var runTime = endUtc - scratch.StartUtc;
            if (runTime < TimeSpan.Zero) runTime = TimeSpan.Zero;

            FlexBackupChunkEntry currentChunk = await _chunkEntryStore.GetByIdAsync(chunkEntryId, cancellationToken)
                ?? throw new InvalidOperationException($"No chunk entry found with ChunkEntryId '{chunkEntryId}'.");

            currentChunk.CompressionMethod = compressionMethod;
            currentChunk.EndDateTimeUtc = endUtc;
            currentChunk.RunTimeMs = (long)runTime.TotalMilliseconds;
            currentChunk.CreateTimeMs = (long)createTime.TotalMilliseconds;
            currentChunk.CompressTimeMs = (long)compressTime.TotalMilliseconds;
            currentChunk.ChunkHash = chunkHash;
            currentChunk.Status = RunStatus.Completed;
            currentChunk.StatusMessage = "Completed";
            currentChunk.FileSize = finalUnCompressedSizeBytes;
            currentChunk.FileSizeCompressed = finalCompressedSizeBytes;
            currentChunk.FileCount = scratch.FileCount;
            currentChunk.CpuTimeMs = (long)cpuTime.TotalMilliseconds;
            currentChunk.CpuPercent = peakCpuPercent;
            currentChunk.MemoryStart = peakWorkingSetBytes;
            currentChunk.MemoryEnd = peakManagedBytes ?? 0;

            await _chunkEntryStore.UpdateAsync(currentChunk, cancellationToken);

            _chunkScratch.TryRemove(chunkEntryId, out _);

            Interlocked.Add(ref _totalChunkBytes, currentChunk.FileSize);
            Interlocked.Add(ref _totalChunkCompressedBytes, currentChunk.FileSizeCompressed);
        }

        /// <summary>
        /// Record that we processed a file into a given chunk.
        /// </summary>
        public async Task RecordFileAsync(string chunkEntryId, string relativePath, string fileHash, CompressionMethod compressionMethod,
            long originalFileSizeBytes, long compressedFileSizeBytes, DateTimeOffset lastWriteTimeUtc, DateTimeOffset fileProcessStartUtc,
            DateTimeOffset fileProcessEndUtc, TimeSpan createTimeMs, TimeSpan cpuTime, double peakCpuPercent, long peakWorkingSetBytes,
            long? peakManagedBytes, CancellationToken cancellationToken = default)
        {
            // hvis run lukker → ignore
            if (!IsRunActive())
                return;

            Interlocked.Increment(ref _totalFiles);
            Interlocked.Add(ref _totalFileBytes, originalFileSizeBytes);
            Interlocked.Add(ref _totalFileCompressedBytes, compressedFileSizeBytes);

            if (!_chunkScratch.TryGetValue(chunkEntryId, out var scratch))
            {
                throw new InvalidOperationException($"RecordFileAsync called for chunk '{chunkEntryId}', but no active chunk state was found. " +
                    "Did you forget to call StartChunkAsync before recording files?");
            }

            Interlocked.Increment(ref scratch.FileCount);

            var runTime = fileProcessEndUtc - fileProcessStartUtc;
            if (runTime < TimeSpan.Zero) runTime = TimeSpan.Zero;

            string backupEntryId;
            lock (_stateLock)
            {
                // vi tjekker igen inden vi bygger row
                if (!IsRunActiveUnsafe())
                    return;

                backupEntryId = _currentBackupEntryId!;
            }

            var fileRow = new FlexBackupFileEntry
            {
                BackupEntryId = backupEntryId,
                ChunkEntryId = chunkEntryId,
                CompressionMethod = compressionMethod,
                Status = RunStatus.Completed,
                StatusMessage = "Completed",
                StartDateTimeUtc = fileProcessStartUtc,
                EndDateTimeUtc = fileProcessEndUtc,
                RunTimeMs = (long)runTime.TotalMilliseconds,
                CreateTimeMs = (long)createTimeMs.TotalMilliseconds,
                CompressTimeMs = 0,
                RelativePath = relativePath,
                LastWriteTimeUtc = lastWriteTimeUtc,
                FileHash = fileHash,
                FileSize = originalFileSizeBytes,
                FileSizeCompressed = compressedFileSizeBytes,
                CpuTimeMs = (long)cpuTime.TotalMilliseconds,
                CpuPercent = peakCpuPercent,
                MemoryStart = peakWorkingSetBytes,
                MemoryEnd = peakManagedBytes ?? 0,
            };

            await _fileEntryStore.InsertAsync(fileRow, cancellationToken);
        }

        // --- existing helper/query methods ---

        public async Task<FlexBackupChunkEntry?> GetFlexBackupChunkEntryByIdAsync(string chunkEntryId)
        {
            FlexBackupChunkEntry? entry = await _chunkEntryStore.GetByIdAsync(chunkEntryId);
            return entry;
        }

        public async Task<FlexBackupEntry?> GetFlexBackupEntryForBackupEntryIdAsync(string backupEntryId)
        {
            FlexBackupEntry? entry = await _backupEntryStore.GetByIdAsync(backupEntryId);
            return entry;
        }

        public async Task<List<FlexBackupEntry>?> GetFlexBackupEntryForJobNameAsync(string jobName)
        {
            List<FlexBackupEntry>? entries = await _backupEntryStore.GetByJobNameAsync(jobName);
            return entries;
        }

        public async Task<List<FlexBackupFileEntry>?> GetFlexBackupFileEntryForBackupEntryIdAsync(string backupEntryId)
        {
            List<FlexBackupFileEntry>? entries = await _fileEntryStore.GetBybackupEntryIdAsync(backupEntryId);
            return entries;
        }

        public async Task<DateTimeOffset?> GetLastJobRunTimeAsync(string jobName)
        {
            return await _backupEntryStore.GetLastJobRunTime(jobName);
        }

        /// <summary>
        /// Old helper – now only used in a couple of places.
        /// </summary>
        private void EnsureRunActive()
        {
            // behold som “hard” check til nogle steder
            if (!IsRunActive())
                throw new InvalidOperationException("No active backup run. Did you call StartRunAsync()?");
        }

        private bool IsRunActive()
        {
            lock (_stateLock)
            {
                return IsRunActiveUnsafe();
            }
        }

        private bool IsRunActiveUnsafe()
        {
            return _runActiveFlag == 1 && !_isClosing && _currentBackupEntryId != null;
        }

        public string? GetCurrentBackupEntryId()
        {
            lock (_stateLock)
            {
                return _currentBackupEntryId;
            }
        }
    }
}