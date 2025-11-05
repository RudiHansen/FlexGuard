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
        public async Task<List<FlexBackupEntry>?> GetByJobNameAsync(string jobName, CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct);
            try { return (await ReadAsync(ct)).Where(x => x.JobName == jobName).ToList(); }
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
            EnsureMax(e.JobName, FlexBackupLimits.JobNameMax, nameof(e.JobName));
            EnsureMax(e.DestinationBackupFolder, FlexBackupLimits.DestinationBackupFolderMax, nameof(e.DestinationBackupFolder));
            EnsureMax(e.StatusMessage, FlexBackupLimits.StatusMessageMax, nameof(e.StatusMessage));
        }
        private static void EnsureMax(string? value, int max, string fieldName)
        {
            if (value is null) return;            // null er ok
            if (value.Length > max)
                throw new ArgumentException($"{fieldName} length must be ≤ {max} characters.", fieldName);
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

            var tmp = Path.Combine(dir, $".{Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");

            await using (var fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(fs, rows, _json, ct).ConfigureAwait(false);
                await fs.FlushAsync(ct).ConfigureAwait(false);
            }

            // Første skrivning: ingen Replace, bare Move (hurtigt og sikkert)
            if (!File.Exists(_path))
            {
                File.Move(tmp, _path);
                return;
            }

            // Sørg for at dest ikke er ReadOnly
            var attrs = File.GetAttributes(_path);
            if ((attrs & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(_path, attrs & ~FileAttributes.ReadOnly);

            const int maxRetries = 5;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    // Ingen backup-fil her – vi holder det hurtigt og enkelt
                    File.Replace(tmp, _path, null, ignoreMetadataErrors: true);
                    return;
                }
                catch (IOException) when (i < maxRetries - 1)
                {
                    // Giv evt. en læser tid til at slippe håndtaget
                    await Task.Delay(25, ct).ConfigureAwait(false);
                }
            }

            // Fallback hvis Replace bliver ved med at fejle
            File.Copy(tmp, _path, overwrite: true);
            File.Delete(tmp);
        }
    }
}