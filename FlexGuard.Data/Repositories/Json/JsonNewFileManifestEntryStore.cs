using System.Collections.Immutable;
using System.Text.Json;
using FlexGuard.Core.Abstractions;
using FlexGuard.Core.Models;

namespace FlexGuard.Data.Repositories.Json;

public sealed class JsonNewFileManifestEntryStore : INewFileManifestEntryStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public JsonNewFileManifestEntryStore(string jsonPath) => _path = jsonPath;

    public async Task<IReadOnlyList<NewFileManifestEntry>> GetAllAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try { return (await ReadAsync(ct)).ToImmutableArray(); }
        finally { _gate.Release(); }
    }

    public async Task<NewFileManifestEntry?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try { return (await ReadAsync(ct)).FirstOrDefault(x => x.Id == id); }
        finally { _gate.Release(); }
    }

    public async Task InsertAsync(NewFileManifestEntry row, CancellationToken ct = default)
    {
        Validate(row);
        await _gate.WaitAsync(ct);
        try
        {
            var list = (await ReadAsync(ct)).ToList();
            if (list.Any(x => x.Id == row.Id))
                throw new InvalidOperationException($"Entry with Id={row.Id} already exists.");
            list.Add(row);
            await WriteAtomicAsync(list, ct);
        }
        finally { _gate.Release(); }
    }

    public async Task UpdateAsync(NewFileManifestEntry row, CancellationToken ct = default)
    {
        Validate(row);
        await _gate.WaitAsync(ct);
        try
        {
            var list = (await ReadAsync(ct)).ToList();
            var idx = list.FindIndex(x => x.Id == row.Id);
            if (idx < 0) throw new InvalidOperationException($"Entry {row.Id} was not found.");
            list[idx] = row;
            await WriteAtomicAsync(list, ct);
        }
        finally { _gate.Release(); }
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var list = (await ReadAsync(ct)).ToList();
            var removed = list.RemoveAll(x => x.Id == id);
            if (removed > 0) await WriteAtomicAsync(list, ct);
        }
        finally { _gate.Release(); }
    }

    // --- Helpers ---

    private static void Validate(NewFileManifestEntry e)
    {
        if (string.IsNullOrWhiteSpace(e.ManifestId))
            throw new ArgumentException("ManifestId is required.", nameof(e));
        if (string.IsNullOrWhiteSpace(e.RelativePath) || e.RelativePath.Length > NewFileManifestLimits.PathMax)
            throw new ArgumentException($"RelativePath must be 1–{NewFileManifestLimits.PathMax} chars.", nameof(e));
        if (string.IsNullOrWhiteSpace(e.ChunkFile) || e.ChunkFile.Length > NewFileManifestLimits.PathMax)
            throw new ArgumentException($"ChunkFile must be 1–{NewFileManifestLimits.PathMax} chars.", nameof(e));
        if (string.IsNullOrWhiteSpace(e.Hash) || e.Hash.Length != NewFileManifestLimits.HashHexLen)
            throw new ArgumentException($"Hash must be {NewFileManifestLimits.HashHexLen} hex chars.", nameof(e));
    }

    private async Task<IReadOnlyList<NewFileManifestEntry>> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(_path)) return [];
        await using var fs = File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<IReadOnlyList<NewFileManifestEntry>>(fs, _json, ct) ?? [];
    }

    private async Task WriteAtomicAsync(IReadOnlyList<NewFileManifestEntry> rows, CancellationToken ct)
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
