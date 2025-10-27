using System.Collections.Immutable;
using System.Text.Json;
using FlexGuard.Core.Abstractions;
using FlexGuard.Core.Models;

namespace FlexGuard.Data.Repositories.Json
{
    public sealed class JsonFlexBackupFileEntryStore : IFlexBackupFileEntryStore
    {
        private readonly string _path;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

        public JsonFlexBackupFileEntryStore(string jsonPath) => _path = jsonPath;

        public async Task<IReadOnlyList<FlexBackupFileEntry>> GetAllAsync(CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct);
            try { return (await ReadAsync(ct)).ToImmutableArray(); }
            finally { _gate.Release(); }
        }

        public async Task<FlexBackupFileEntry?> GetByIdAsync(string fileEntryId, CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct);
            try { return (await ReadAsync(ct)).FirstOrDefault(x => x.FileEntryId == fileEntryId); }
            finally { _gate.Release(); }
        }

        public async Task InsertAsync(FlexBackupFileEntry row, CancellationToken ct = default)
        {
            Validate(row);
            await _gate.WaitAsync(ct);
            try
            {
                var list = (await ReadAsync(ct)).ToList();
                if (list.Any(x => x.FileEntryId == row.FileEntryId))
                    throw new InvalidOperationException($"FileEntry {row.FileEntryId} already exists.");

                list.Add(row);
                await WriteAtomicAsync(list, ct);
            }
            finally { _gate.Release(); }
        }

        public async Task UpdateAsync(FlexBackupFileEntry row, CancellationToken ct = default)
        {
            Validate(row);
            await _gate.WaitAsync(ct);
            try
            {
                var list = (await ReadAsync(ct)).ToList();
                var idx = list.FindIndex(x => x.FileEntryId == row.FileEntryId);
                if (idx < 0)
                    throw new InvalidOperationException($"FileEntry {row.FileEntryId} not found.");

                list[idx] = row;
                await WriteAtomicAsync(list, ct);
            }
            finally { _gate.Release(); }
        }

        public async Task DeleteAsync(string fileEntryId, CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct);
            try
            {
                var list = (await ReadAsync(ct)).ToList();
                var removed = list.RemoveAll(x => x.FileEntryId == fileEntryId);
                if (removed > 0)
                    await WriteAtomicAsync(list, ct);
            }
            finally { _gate.Release(); }
        }

        private static void Validate(FlexBackupFileEntry e)
        {
            if (string.IsNullOrWhiteSpace(e.ChunkEntryId))
            {
                throw new ArgumentException(
                    "ChunkEntryId is required.",
                    nameof(e));
            }

            if (string.IsNullOrWhiteSpace(e.BackupEntryId))
            {
                throw new ArgumentException(
                    "BackupEntryId is required.",
                    nameof(e));
            }

            if (string.IsNullOrWhiteSpace(e.RelativePath) ||
                e.RelativePath.Length > FlexBackupLimits.RelativePathMax)
            {
                throw new ArgumentException(
                    $"RelativePath must be 1–{FlexBackupLimits.RelativePathMax} chars.",
                    nameof(e));
            }

            if (string.IsNullOrWhiteSpace(e.FileHash) ||
                e.FileHash.Length != FlexBackupLimits.HashHexLen)
            {
                throw new ArgumentException(
                    $"FileHash must be {FlexBackupLimits.HashHexLen} hex chars.",
                    nameof(e));
            }

            if (e.StatusMessage is { Length: > FlexBackupLimits.StatusMessageMax })
            {
                throw new ArgumentException(
                    $"StatusMessage must be ≤ {FlexBackupLimits.StatusMessageMax} chars.",
                    nameof(e));
            }

            if (e.RunTimeMs < 0 ||
                e.CreateTimeMs < 0 ||
                e.CompressTimeMs < 0 ||
                e.CpuTimeMs < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(e),
                    "Timing values must be >= 0.");
            }

            if (e.FileSize < 0 ||
                e.FileSizeCompressed < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(e),
                    "FileSize values must be >= 0.");
            }

            if (e.CpuPercent < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(e),
                    "CpuPercent must be >= 0.");
            }
        }


        private async Task<IReadOnlyList<FlexBackupFileEntry>> ReadAsync(CancellationToken ct)
        {
            if (!File.Exists(_path)) return [];
            await using var fs = File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await JsonSerializer.DeserializeAsync<IReadOnlyList<FlexBackupFileEntry>>(fs, _json, ct)
                   ?? [];
        }

        private async Task WriteAtomicAsync(IReadOnlyList<FlexBackupFileEntry> rows, CancellationToken ct)
        {
            var dir = Path.GetDirectoryName(_path)!;
            Directory.CreateDirectory(dir);

            var tmp = _path + ".tmp";
            var bak = _path + ".bak";

            await using (var fs = File.Open(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(fs, rows, _json, ct);
                await fs.FlushAsync(ct);
            }

            if (!File.Exists(_path)) { File.Move(tmp, _path); return; }
            File.Replace(tmp, _path, bak, true);
        }
    }
}