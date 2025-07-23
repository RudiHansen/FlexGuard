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
                            CompressionRatio = 0
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