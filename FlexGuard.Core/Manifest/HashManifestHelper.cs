using System.Text.Json;

namespace FlexGuard.Core.Manifest;
public class HashManifestHelper
{
    private readonly Dictionary<string, string> _chunkHashes;
    private readonly HashSet<string> _alreadyReturned;

    public HashManifestHelper(string manifestFilePath)
    {
        _alreadyReturned = new(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(manifestFilePath))
        {
            _chunkHashes = new();
            return;
        }

        var json = File.ReadAllText(manifestFilePath);
        var manifest = JsonSerializer.Deserialize<HashManifest>(json);

        _chunkHashes = manifest?.Chunks
            .ToDictionary(c => c.ChunkFile, c => c.Hash, StringComparer.OrdinalIgnoreCase)
            ?? new();
    }

    public string GetChunkHash(string chunkFile)
    {
        if (!_chunkHashes.TryGetValue(chunkFile, out var hash))
            return "";

        if (!_alreadyReturned.Add(chunkFile))
            return "";

        return hash;
    }
}