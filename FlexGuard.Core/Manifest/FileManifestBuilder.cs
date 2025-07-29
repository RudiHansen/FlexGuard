using FlexGuard.Core.Compression;
using FlexGuard.Core.Options;
using FlexGuard.Core.Util;
using System.Text.Json;

namespace FlexGuard.Core.Manifest;

public class FileManifestBuilder
{
    private readonly FileManifest _manifest;
    public CompressionMethod Compression => _manifest.Compression;

    public FileManifestBuilder(string jobName, OperationMode mode, DateTime timestamp, CompressionMethod compression)
    {
        _manifest = new FileManifest
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
        var fileName = $"filemanifest_{_manifest.Timestamp:yyyy-MM-ddTHHmm}.json";
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