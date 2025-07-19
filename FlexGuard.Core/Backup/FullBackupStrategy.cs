using FlexGuard.Core.Compression;
using FlexGuard.Core.Hashing;
using FlexGuard.Core.Manifest;
using FlexGuard.Core.Config;
using FlexGuard.Core.Util;
using System.Text.Json;

namespace FlexGuard.Core.Backup;

public class FullBackupStrategy : IBackupStrategy
{
    private readonly ICompressor _compressor;
    private readonly IHasher _hasher;

    public FullBackupStrategy(ICompressor compressor, IHasher hasher)
    {
        _compressor = compressor;
        _hasher = hasher;
    }

    public void RunBackup(BackupConfig config)
    {
        var manifest = new BackupManifest
        {
            Type = "Full",
            Timestamp = DateTime.UtcNow,
            Files = new List<FileEntry>()
        };

        foreach (var source in config.Sources)
        {
            var files = FileEnumerator.GetFiles(source.Path, source.Exclude);
            foreach (var file in files)
            {
                string relativePath = Path.GetRelativePath(source.Path, file);
                string hash = _hasher.ComputeHash(file);
                string destName = $"{relativePath.Replace(Path.DirectorySeparatorChar, '_')}.gz";
                string destPath = Path.Combine(config.DestinationPath, destName);

                _compressor.Compress(file, destPath);

                manifest.Files.Add(new FileEntry
                {
                    SourcePath = file,
                    RelativePath = relativePath,
                    Hash = hash,
                    CompressedFileName = destName
                });
            }
        }

        string manifestPath = Path.Combine(config.DestinationPath, "manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }
}