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
            using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            using var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal);
            using var archive = new ZipArchive(gzipStream, ZipArchiveMode.Create);

            foreach (var file in group.Files)
            {
                try
                {
                    // Læs og hash filens indhold
                    using var sourceStream = new FileStream(file.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var ms = new MemoryStream();
                    sourceStream.CopyTo(ms);
                    var fileBytes = ms.ToArray();

                    string hash;
                    using (var sha256 = SHA256.Create())
                    {
                        hash = Convert.ToHexString(sha256.ComputeHash(fileBytes));
                    }

                    // Opret entry og skriv data
                    var entry = archive.CreateEntry(file.RelativePath, CompressionLevel.Optimal);
                    long compressedSize;
                    using (var entryStream = entry.Open())
                    {
                        ms.Position = 0;
                        ms.CopyTo(entryStream);
                    }

                    // Efter entry-stream er lukket, kan vi få længden
                    compressedSize = entry.CompressedLength;

                    // Beregn komprimeringsrate
                    double compressionRatio = file.FileSize > 0
                        ? Math.Round(100.0 * (file.FileSize - compressedSize) / file.FileSize, 2)
                        : 0;

                    // Tilføj til manifest
                    manifestBuilder.AddFile(new FileEntry
                    {
                        RelativePath = file.RelativePath,
                        Hash = hash,
                        ChunkFile = chunkFileName,
                        FileSize = file.FileSize,
                        LastWriteTimeUtc = file.LastWriteTimeUtc,
                        CompressionSkipped = false,
                        CompressionRatio = compressedSize
                    });
                }
                catch (Exception ex)
                {
                    reporter.Warning($"Failed to add file '{file.SourcePath}': {ex.Message}");
                }
            }

            reporter.Info($"Chunk {group.Index} written to '{outputPath}'");
        }
        catch (Exception ex)
        {
            reporter.Error($"Failed to create chunk {group.Index}: {ex.Message}");
        }
    }
}