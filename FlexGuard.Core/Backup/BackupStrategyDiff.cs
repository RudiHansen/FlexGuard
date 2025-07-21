using FlexGuard.Core.Config;
using FlexGuard.Core.Manifest;
using FlexGuard.Core.Reporting;
using FlexGuard.Core.Util;
using System.Text.Json;

namespace FlexGuard.Core.Backup;

public class BackupStrategyDiff : IBackupStrategy
{
    private readonly IBackupProcessor _processor;
    private readonly IMessageReporter _reporter;
    private readonly Dictionary<string, FileEntry> _previousEntries;

    public BackupStrategyDiff(IBackupProcessor processor, BackupManifest previousManifest, IMessageReporter reporter)
    {
        _processor = processor;
        _reporter = reporter;

        _previousEntries = previousManifest.Files?
            .ToDictionary(f => NormalizePath(f.RelativePath))
            ?? new();
    }

    public void RunBackup(BackupJobConfig config, string destinationPath, IMessageReporter reporter)
    {
        var manifest = new BackupManifest
        {
            Type = "Diff",
            Timestamp = DateTime.UtcNow,
            Files = new List<FileEntry>()
        };

        foreach (var source in config.Sources)
        {
            var files = FileEnumerator.GetFiles(source.Path, source.Exclude,reporter).ToList();

            var diffFiles = files
                .Where(file => ShouldIncludeFile(file, source.Path))
                .ToList();

            _processor.ProcessFiles(diffFiles, source.Path, destinationPath, manifest.Files, reporter);
        }

        string manifestPath = Path.Combine(destinationPath, "manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }

    private bool ShouldIncludeFile(string filePath, string sourceRoot)
    {
        try
        {
            var info = new FileInfo(filePath);
            var relativePath = Path.GetRelativePath(sourceRoot, filePath);
            var normalizedPath = NormalizePath(relativePath);

            if (_previousEntries.TryGetValue(normalizedPath, out var oldEntry))
            {
                return oldEntry.FileSize != info.Length ||
                       oldEntry.LastWriteTimeUtc != info.LastWriteTimeUtc;
            }

            return true; // New file
        }
        catch (Exception ex)
        {
            _reporter.Warning($"Error checking file {filePath}: {ex.Message}");
            return true;
        }
    }

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').ToLowerInvariant();
}
