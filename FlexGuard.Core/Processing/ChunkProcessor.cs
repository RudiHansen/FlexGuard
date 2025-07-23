using FlexGuard.Core.Manifest;
using FlexGuard.Core.Model;
using FlexGuard.Core.Options;
using FlexGuard.Core.Processing;
using FlexGuard.Core.Reporting;
using System.IO.Compression;
using System.Security.Cryptography;

public static class ChunkProcessor
{
    public static void Process(
        FileGroup group,
        string backupFolderPath,
        ProgramOptions options,
        IMessageReporter reporter,
        BackupManifestBuilder manifestBuilder)
    {
        const long MaxSizeForRatioCheck = 100_000_000; // 100 MB

        var chunkFileName = $"{group.Index:D4}.fgchunk";
        var outputPath = Path.Combine(backupFolderPath, chunkFileName);

        Directory.CreateDirectory(backupFolderPath);
        reporter.Info($"Processing chunk {group.Index} with {group.Files.Count} files...");

        try
        {
            using var zipBuffer = new MemoryStream();
            using (var archive = new ZipArchive(zipBuffer, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var file in group.Files)
                {
                    try
                    {
                        if (options.EnableCompressionRatioMeasurement && file.FileSize > MaxSizeForRatioCheck)
                        {
                            reporter.Debug($"Large file detected: {file.RelativePath} ({file.FileSize / 1024 / 1024} MB). Skipping compression ratio measurement.");
                        }

                        using var sourceStream = new FileStream(file.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        using var ms = new MemoryStream();
                        sourceStream.CopyTo(ms);
                        var fileBytes = ms.ToArray();

                        // Calculate SHA256 hash
                        string hash;
                        using (var sha256 = SHA256.Create())
                        {
                            hash = Convert.ToHexString(sha256.ComputeHash(fileBytes));
                        }

                        // Optional compression ratio measurement
                        double compressionRatio = 0;
                        if (options.EnableCompressionRatioMeasurement && file.FileSize <= MaxSizeForRatioCheck)
                        {
                            using var tempCompressed = new MemoryStream();
                            using (var zip = new ZipArchive(tempCompressed, ZipArchiveMode.Create, leaveOpen: true))
                            {
                                var tempEntry = zip.CreateEntry("dummy", CompressionLevel.Optimal);
                                using var tempStream = tempEntry.Open();
                                tempStream.Write(fileBytes, 0, fileBytes.Length);
                            }

                            long compressedSize = tempCompressed.Length;
                            compressionRatio = file.FileSize > 0
                                ? Math.Round(100.0 * (file.FileSize - compressedSize) / file.FileSize, 2)
                                : 0;
                        }

                        // Add real entry to archive
                        var entry = archive.CreateEntry(file.RelativePath, CompressionLevel.Optimal);
                        using (var entryStream = entry.Open())
                        {
                            entryStream.Write(fileBytes, 0, fileBytes.Length);
                        }

                        // Add to manifest
                        manifestBuilder.AddFile(new FileEntry
                        {
                            RelativePath = file.RelativePath,
                            Hash = hash,
                            ChunkFile = chunkFileName,
                            FileSize = file.FileSize,
                            LastWriteTimeUtc = file.LastWriteTimeUtc,
                            CompressionSkipped = false,
                            CompressionRatio = compressionRatio
                        });
                    }
                    catch (Exception ex)
                    {
                        reporter.Warning($"Failed to add file '{file.SourcePath}': {ex.Message}");
                    }
                }
            }

            // Write to disk as GZip
            zipBuffer.Position = 0;
            using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            using var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal);
            zipBuffer.CopyTo(gzipStream);

            reporter.Info($"Chunk {group.Index} written to '{outputPath}'");
        }
        catch (Exception ex)
        {
            reporter.Error($"Failed to create chunk {group.Index}: {ex.Message}");
        }
    }
}