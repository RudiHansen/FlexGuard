using System.Collections.Immutable;
using System.Text.Json;
using FlexGuard.Core.Abstractions;
using FlexGuard.Core.Models;

namespace FlexGuard.Data.Repositories.Json;

public sealed class JsonNewFileManifestStore : INewFileManifestStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public JsonNewFileManifestStore(string jsonPath) => _path = jsonPath;

    public async Task<IReadOnlyList<NewFileManifest>> GetAllAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try { return (await ReadAsync(ct)).ToImmutableArray(); }
        finally { _gate.Release(); }
    }

    public async Task<NewFileManifest?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try { return (await ReadAsync(ct)).FirstOrDefault(x => x.Id == id); }
        finally { _gate.Release(); }
    }

    public async Task InsertAsync(NewFileManifest row, CancellationToken ct = default)
    {
        Validate(row);
        await _gate.WaitAsync(ct);
        try
        {
            var list = (await ReadAsync(ct)).ToList();
            if (list.Any(x => x.Id == row.Id))
                throw new InvalidOperationException($"Manifest with Id={row.Id} already exists.");
            list.Add(row);
            await WriteAtomicAsync(list, ct);
        }
        finally { _gate.Release(); }
    }

    public async Task UpdateAsync(NewFileManifest row, CancellationToken ct = default)
    {
        Validate(row);
        await _gate.WaitAsync(ct);
        try
        {
            var list = (await ReadAsync(ct)).ToList();
            var idx = list.FindIndex(x => x.Id == row.Id);
            if (idx < 0) throw new InvalidOperationException($"Manifest {row.Id} was not found.");
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

    private static void Validate(NewFileManifest m)
    {
        if (string.IsNullOrWhiteSpace(m.JobName) || m.JobName.Length > NewFileManifestLimits.JobNameMax)
            throw new ArgumentException($"JobName must be 1–{NewFileManifestLimits.JobNameMax} chars.", nameof(m));
        // evt. flere regler senere
    }

    private async Task<IReadOnlyList<NewFileManifest>> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(_path)) return [];
        await using var fs = File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<IReadOnlyList<NewFileManifest>>(fs, _json, ct) ?? [];
    }

    private async Task WriteAtomicAsync(IReadOnlyList<NewFileManifest> rows, CancellationToken ct)
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