using FlexGuard.Core.Util;
using System.Text.Json;

namespace FlexGuard.Core.Manifest;

public class HashManifestBuilder
{
    private readonly HashManifest _manifest;

    public HashManifestBuilder(string jobName, DateTime timestamp, string hashAlgorithm = "SHA256")
    {
        _manifest = new HashManifest
        {
            JobName = jobName,
            Timestamp = timestamp,
            HashAlgorithm = hashAlgorithm
        };
    }

    public void AddChunk(ChunkHashEntry entry)
    {
        _manifest.Chunks.Add(entry);
    }

    public string Save(string destinationFolder)
    {
        var fileName = $"hashmanifest_{_manifest.Timestamp:yyyy-MM-ddTHHmm}.json";
        var fullPath = Path.Combine(destinationFolder, fileName);

        if (!string.IsNullOrEmpty(destinationFolder))
        {
            Directory.CreateDirectory(destinationFolder);
        }

        var json = JsonSerializer.Serialize(_manifest, JsonSettings.Indented);
        File.WriteAllText(fullPath, json);

        return fileName;
    }
}