using FlexGuard.Core.Compression;
using FlexGuard.Core.Manifest;
using FlexGuard.Core.Options;
using FlexGuard.Core.Util;
using System.Text.Json;

namespace FlexGuard.Core.Backup;

public class BackupManifestBuilder
{
    private readonly BackupManifest _manifest;
    public CompressionMethod Compression => _manifest.Compression;

    public BackupManifestBuilder(string jobName, OperationMode mode, DateTime timestamp, CompressionMethod compression)
    {
        _manifest = new BackupManifest
        {
            JobName = jobName,
            Type = mode.ToString(),
            Timestamp = timestamp,
            Compression = compression
        };
    }

    public void AddFile(FileEntry entry)
    {
        _manifest.Files.Add(entry);
    }

    public string Save(string destinationFolder)
    {
        var fileName = $"manifest_{_manifest.Timestamp:yyyy-MM-ddTHHmm}.json";
        var fullPath = Path.Combine(destinationFolder, fileName);

        if (!string.IsNullOrEmpty(destinationFolder))
        {
            Directory.CreateDirectory(destinationFolder); // Sørger for at mappen findes
        }

        var json = JsonSerializer.Serialize(_manifest, JsonSettings.Indented);

        File.WriteAllText(fullPath, json);
        return fileName;
    }
}
