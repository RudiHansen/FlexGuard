using FlexGuard.Core.Config;
using FlexGuard.Core.Manifest;
using FlexGuard.Core.Util;
using System.Text.Json;

namespace FlexGuard.Core.Backup;

public class BackupStrategyFull : IBackupStrategy
{
    private readonly IBackupProcessor _processor;

    public BackupStrategyFull(IBackupProcessor processor)
    {
        _processor = processor;
    }

    public void RunBackup(BackupConfig config, string destinationPath, Action<int, int, string>? reportProgress = null)
    {
        var manifest = new BackupManifest
        {
            Type = "Full",
            Timestamp = DateTime.UtcNow,
            Files = new List<FileEntry>()
        };

        foreach (var source in config.Sources)
        {
            var files = FileEnumerator.GetFiles(source.Path, source.Exclude).ToList();
            _processor.ProcessFiles(files, source.Path, destinationPath, manifest.Files);
        }

        string manifestPath = Path.Combine(destinationPath, "manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }
}
