using System.Collections.Immutable;
using System.Text.Json;
using FlexGuard.Core.Abstractions;
using FlexGuard.Core.Models;

namespace FlexGuard.Data.Repositories.Json
{
    public sealed class JsonFlexBackupEntryStore : IFlexBackupEntryStore
    {
        private readonly string _path;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

        public JsonFlexBackupEntryStore(string jsonPath) => _path = jsonPath;

        public async Task<IReadOnlyList<FlexBackupEntry>> GetAllAsync(CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct);
            try { return (await ReadAsync(ct)).ToImmutableArray(); }
            finally { _gate.Release(); }
        }

        public async Task<FlexBackupEntry?> GetByIdAsync(string backupEntryId, CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct);
            try { return (await ReadAsync(ct)).FirstOrDefault(x => x.BackupEntryId == backupEntryId); }
            finally { _gate.Release(); }
        }

        public async Task InsertAsync(FlexBackupEntry row, CancellationToken ct = default)
        {
            Validate(row);
            await _gate.WaitAsync(ct);
            try
            {
                var list = (await ReadAsync(ct)).ToList();
                if (list.Any(x => x.BackupEntryId == row.BackupEntryId))
                    throw new InvalidOperationException($"BackupEntry {row.BackupEntryId} already exists.");

                list.Add(row);
                await WriteAtomicAsync(list, ct);
            }
            finally { _gate.Release(); }
        }

        public async Task UpdateAsync(FlexBackupEntry row, CancellationToken ct = default)
        {
            Validate(row);
            await _gate.WaitAsync(ct);
            try
            {
                var list = (await ReadAsync(ct)).ToList();
                var idx = list.FindIndex(x => x.BackupEntryId == row.BackupEntryId);
                if (idx < 0)
                    throw new InvalidOperationException($"BackupEntry {row.BackupEntryId} not found.");

                list[idx] = row;
                await WriteAtomicAsync(list, ct);
            }
            finally { _gate.Release(); }
        }

        public async Task DeleteAsync(string backupEntryId, CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct);
            try
            {
                var list = (await ReadAsync(ct)).ToList();
                var removed = list.RemoveAll(x => x.BackupEntryId == backupEntryId);
                if (removed > 0)
                    await WriteAtomicAsync(list, ct);
            }
            finally { _gate.Release(); }
        }

        // ---- helpers ----

        private static void Validate(FlexBackupEntry e)
        {
            if (string.IsNullOrWhiteSpace(e.JobName) || e.JobName.Length > FlexBackupLimits.JobNameMax)
                throw new ArgumentException($"JobName must be 1–{FlexBackupLimits.JobNameMax} chars.", nameof(e));

            if (e.StatusMessage is { Length: > FlexBackupLimits.StatusMessageMax })
                throw new ArgumentException($"StatusMessage must be ≤ {FlexBackupLimits.StatusMessageMax} chars.", nameof(e));

            if (e.TotalFiles < 0 || e.TotalGroups < 0 || e.TotalBytes < 0 || e.TotalBytesCompressed < 0)
                throw new ArgumentOutOfRangeException(nameof(e), "Totals must be >= 0.");

            if (e.RunTimeMs < 0)
                throw new ArgumentOutOfRangeException(nameof(e), "RunTimeMs must be >= 0.");

            if (e.CompressionRatio < 0)
                throw new ArgumentOutOfRangeException(nameof(e), "CompressionRatio must be >= 0.");
        }

        private async Task<IReadOnlyList<FlexBackupEntry>> ReadAsync(CancellationToken ct)
        {
            if (!File.Exists(_path)) return [];
            await using var fs = File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await JsonSerializer.DeserializeAsync<IReadOnlyList<FlexBackupEntry>>(fs, _json, ct)
                   ?? [];
        }

        private async Task WriteAtomicAsync(IReadOnlyList<FlexBackupEntry> rows, CancellationToken ct)
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