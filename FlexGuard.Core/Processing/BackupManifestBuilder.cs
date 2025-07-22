using System.Text.Json;
using FlexGuard.Core.Manifest;
using FlexGuard.Core.Model;
using FlexGuard.Core.Options;

namespace FlexGuard.Core.Processing;

public class BackupManifestBuilder
{
    private readonly BackupManifest _manifest;

    public BackupManifestBuilder(string jobName, OperationMode mode)
    {
        _manifest = new BackupManifest
        {
            JobName = jobName,
            Type = mode.ToString(),
            Timestamp = DateTime.UtcNow
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

        var json = JsonSerializer.Serialize(_manifest, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(fullPath, json);
        return fileName;
    }
}
