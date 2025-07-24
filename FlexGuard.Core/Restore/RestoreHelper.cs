using FlexGuard.Core.Config;
using FlexGuard.Core.Manifest;
using FlexGuard.Core.Model;
using FlexGuard.Core.Registry;
using FlexGuard.Core.Reporting;
using System.IO.Compression;
using System.Text.Json;

namespace FlexGuard.Core.Restore;

public static class RestoreHelper
{
    public static void RestoreFile(
        string relativePath,
        string manifestPath,
        BackupJobConfig jobConfig,
        IMessageReporter reporter)
    {
        if (string.IsNullOrWhiteSpace(jobConfig.RestoreTargetFolder))
        {
            reporter.Error("RestoreTargetFolder is not defined in the job config.");
            return;
        }

        // Load manifest
        var localJobsFolder = Path.Combine(AppContext.BaseDirectory, "Jobs", jobConfig.JobName);
        var registryManager = new BackupRegistryManager(jobConfig.JobName, localJobsFolder);
        // Trin 1: Find den nyeste registry-post
        var latestEntry = registryManager.GetLatestEntry("FullBackup");
        if (latestEntry == null)
        {
            reporter.Error("No backups found.");
            return;
        }

        // Trin 2: Indlæs manifestet fra fil
        var manifest = Load(Path.Combine(manifestPath,latestEntry.ManifestFileName));

        // Trin 3: Find filen du vil gendanne
        var entry = manifest.Files.FirstOrDefault(f => f.RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase));

        if (entry == null)
        {
            reporter.Error($"File '{relativePath}' not found in manifest.");
            return;
        }

        // Locate chunk file
        var chunkPath = Path.Combine(jobConfig.DestinationPath, latestEntry.DestinationFolderName, entry.ChunkFile);
        if (!File.Exists(chunkPath))
        {
            reporter.Error($"Chunk file '{chunkPath}' not found.");
            return;
        }

        // Extract ZIP from GZIP
        using var chunkStream = new FileStream(chunkPath, FileMode.Open, FileAccess.Read);
        using var gzipStream = new GZipStream(chunkStream, CompressionMode.Decompress);
        using var zipArchive = new ZipArchive(gzipStream, ZipArchiveMode.Read);

        var zipEntry = zipArchive.GetEntry(relativePath);
        if (zipEntry == null)
        {
            reporter.Error($"Entry '{relativePath}' not found in chunk archive.");
            return;
        }

        // Determine output path
        //var outputPath = Path.Combine(jobConfig.RestoreTargetFolder, relativePath);
        var outputPath = Path.GetFullPath(Path.Combine(jobConfig.RestoreTargetFolder, relativePath));
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        // Extract file
        using var entryStream = zipEntry.Open();
        using var outputFile = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        entryStream.CopyTo(outputFile);

        reporter.Info($"Restored file to: {outputPath}");
    }
    public static BackupManifest Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<BackupManifest>(json)!;
    }
}