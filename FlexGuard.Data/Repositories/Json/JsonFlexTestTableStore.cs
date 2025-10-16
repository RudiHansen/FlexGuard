using System.Collections.Immutable;
using System.Text.Json;
using FlexGuard.Core.Abstractions;
using FlexGuard.Core.Models;

namespace FlexGuard.Data.Repositories.Json;

public sealed class JsonFlexTestTableStore : IFlexTestTableStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public JsonFlexTestTableStore(string jsonPath) => _path = jsonPath;

    public async Task<IReadOnlyList<FlexTestRow>> GetAllAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var rows = await ReadAsync(ct);
            return rows.ToImmutableArray();
        }
        finally { _gate.Release(); }
    }

    public async Task<FlexTestRow?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var rows = await ReadAsync(ct);
            return rows.FirstOrDefault(r => r.Id == id);
        }
        finally { _gate.Release(); }
    }
    public async Task InsertAsync(FlexTestRow row, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(row.TestNavn) || row.TestNavn.Length > DomainLimits.TestNavnMax)
            throw new ArgumentException($"'{nameof(row.TestNavn)}' must be ≤ {DomainLimits.TestNavnMax} characters.", nameof(row));

        await _gate.WaitAsync(ct);
        try
        {
            var list = (await ReadAsync(ct)).ToList();
            if (list.Any(r => r.Id == row.Id))
                throw new InvalidOperationException($"Row with Id={row.Id} already exists.");

            list.Add(row);
            await WriteAtomicAsync(list, ct);
        }
        finally { _gate.Release(); }
    }

    public async Task UpdateAsync(FlexTestRow row, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(row.TestNavn) || row.TestNavn.Length > DomainLimits.TestNavnMax)
            throw new ArgumentException($"'{nameof(row.TestNavn)}' must be ≤ {DomainLimits.TestNavnMax} characters.", nameof(row));

        await _gate.WaitAsync(ct);
        try
        {
            var list = (await ReadAsync(ct)).ToList();
            var idx = list.FindIndex(r => r.Id == row.Id);
            if (idx < 0)
                throw new InvalidOperationException($"Row with Id={row.Id} was not found.");

            list[idx] = row;
            await WriteAtomicAsync(list, ct);
        }
        finally { _gate.Release(); }
    }
    public async Task UpsertAsync(FlexTestRow row, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(row.TestNavn) || row.TestNavn.Length > DomainLimits.TestNavnMax)
            throw new ArgumentException(
                $"'{nameof(row.TestNavn)}' must be ≤ {DomainLimits.TestNavnMax} characters.",
                nameof(row));

        await _gate.WaitAsync(ct);
        try
        {
            var list = (await ReadAsync(ct)).ToList();
            var idx = list.FindIndex(r => r.Id == row.Id);
            if (idx >= 0) list[idx] = row; else list.Add(row);
            await WriteAtomicAsync(list, ct);
        }
        finally { _gate.Release(); }
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var list = (await ReadAsync(ct)).ToList();
            var removed = list.RemoveAll(r => r.Id == id);
            if (removed > 0) await WriteAtomicAsync(list, ct);
        }
        finally { _gate.Release(); }
    }

    // --- Helpers ---

    private async Task<IReadOnlyList<FlexTestRow>> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(_path)) return [];
        await using var fs = File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var data = await JsonSerializer.DeserializeAsync<IReadOnlyList<FlexTestRow>>(fs, _json, ct);
        return data ?? [];
    }

    private async Task WriteAtomicAsync(IReadOnlyList<FlexTestRow> rows, CancellationToken ct)
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

        // Første gang: destination findes ikke -> Move
        if (!File.Exists(_path))
        {
            File.Move(tmp, _path);
            return;
        }

        // Efterfølgende: atomisk udskiftning + backup
        File.Replace(tmp, _path, bak, ignoreMetadataErrors: true);
        // (valgfrit) Ryd backup'en:
        // try { File.Delete(bak); } catch { /* ignore */ }
    }
}
