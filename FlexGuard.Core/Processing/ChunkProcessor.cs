using FlexGuard.Core.Manifest;
using FlexGuard.Core.Model;
using FlexGuard.Core.Options;
using FlexGuard.Core.Reporting;
using System.IO.Compression;
using System.Security.Cryptography;

namespace FlexGuard.Core.Processing;

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
        reporter.Info($"Processing chunk {group.Index} with {group.Files.Count} files ({group.GroupType})...");

        // Determine ZIP compression level based on group type
        var zipCompressionLevel = group.GroupType switch
        {
            FileGroupType.LargeNonCompressible or FileGroupType.HugeNonCompressible or FileGroupType.SmallNonCompressible =>
                CompressionLevel.NoCompression,
            _ =>
                CompressionLevel.Optimal
        };

        // Flag to control final GZip wrapping
        bool skipGZip = group.GroupType is FileGroupType.HugeCompressible or FileGroupType.HugeNonCompressible;

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

                        // Add file to ZIP archive
                        var entry = archive.CreateEntry(file.RelativePath, zipCompressionLevel);
                        using (var entryStream = entry.Open())
                        {
                            entryStream.Write(fileBytes, 0, fileBytes.Length);
                        }

                        manifestBuilder.AddFile(new FileEntry
                        {
                            RelativePath = file.RelativePath,
                            Hash = hash,
                            ChunkFile = chunkFileName,
                            FileSize = file.FileSize,
                            LastWriteTimeUtc = file.LastWriteTimeUtc,
                            CompressionSkipped = zipCompressionLevel == CompressionLevel.NoCompression,
                            CompressionRatio = 0
                        });
                    }
                    catch (Exception ex)
                    {
                        reporter.Warning($"Failed to add file '{file.SourcePath}': {ex.Message}");
                    }
                }
            }

            // Write buffer to disk with optional GZip compression
            zipBuffer.Position = 0;
            using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            if (skipGZip)
            {
                zipBuffer.CopyTo(fileStream);
            }
            else
            {
                using var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal);
                zipBuffer.CopyTo(gzipStream);
            }

            reporter.Info($"Chunk {group.Index} written to '{outputPath}'");
        }
        catch (Exception ex)
        {
            reporter.Error($"Failed to create chunk {group.Index}: {ex.Message}");
        }
    }
}