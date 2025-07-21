using FlexGuard.Core.Config;
using FlexGuard.Core.Manifest;
using FlexGuard.Core.Reporting;
using FlexGuard.Core.Util;
using System.Text.Json;

namespace FlexGuard.Core.Backup;

public class BackupStrategyFull : IBackupStrategy
{
    private readonly IBackupProcessor _processor;
    private readonly IMessageReporter _reporter;

    public BackupStrategyFull(IBackupProcessor processor, IMessageReporter reporter)
    {
        _processor = processor;
        _reporter = reporter;
    }

    public void RunBackup(BackupJobConfig config, string destinationPath, IMessageReporter reporter)
    {
        var manifest = new BackupManifest
        {
            Type = "Full",
            Timestamp = DateTime.UtcNow,
            Files = new List<FileEntry>()
        };

        foreach (var source in config.Sources)
        {
            var files = FileEnumerator.GetFiles(source.Path, source.Exclude, reporter).ToList();
            _processor.ProcessFiles(files, source.Path, destinationPath, manifest.Files, _reporter);
        }

        string manifestPath = Path.Combine(destinationPath, "manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }
}
