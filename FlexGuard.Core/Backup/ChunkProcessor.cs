using FlexGuard.Core.Compression;
using FlexGuard.Core.Manifest;
using FlexGuard.Core.Profiling;
using FlexGuard.Core.Recording;
using FlexGuard.Core.Reporting;
using FlexGuard.Core.Util;
using System.IO.Compression;

namespace FlexGuard.Core.Backup;

public static class ChunkProcessor
{
    public static async Task ProcessAsync(
        FileGroup group,
        string backupFolderPath,
        IMessageReporter reporter,
        FileManifestBuilder fileManifestBuilder,
        HashManifestBuilder hashManifestBuilder,
        BackupRunRecorder recorder)
    {
        var chunkFileName = $"{group.Index:D4}.fgchunk";
        var outputPath = Path.Combine(backupFolderPath, chunkFileName);

        Directory.CreateDirectory(backupFolderPath);

        // Determine ZIP compression level based on group type (internal to the ZIP)
        var zipCompressionLevel = CompressionLevel.NoCompression;

        // Select outer compressor based on the manifest (GZip, Brotli, Zstd)
        var compressor = CompressorFactory.Create(fileManifestBuilder.Compression);

        // Step 1: Create temporary zip file
        var tempZipPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        string chunkHash        = string.Empty;
        long compressedSize     = 0;
        TimeSpan createTime     = TimeSpan.Zero; // TODO: Implement time measurement
        TimeSpan compressTime   = TimeSpan.Zero; // TODO: Implement time measurement
        try
        {
            string chunkEntryId = await recorder.StartChunkAsync(chunkFileName, fileManifestBuilder.Compression, group.Index);
            using (var scope = PerformanceTracker.TrackSection("Creating Chunk"))
            {
                scope.Set("chunkIndex", group.Index);
                using var zipFileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write);
                using var archive = new ZipArchive(zipFileStream, ZipArchiveMode.Create, leaveOpen: false);
                foreach (var file in group.Files)
                {
                    try
                    {
                        DateTimeOffset fileProcessStartUtc = DateTimeOffset.UtcNow;
                        using var sourceStream = new FileStream(file.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                        // Compute hash directly from stream
                        string hash = HashHelper.ComputeHash(sourceStream);

                        var entry = archive.CreateEntry(file.RelativePath, zipCompressionLevel);
                        using var entryStream = entry.Open();
                        sourceStream.CopyTo(entryStream);

                        fileManifestBuilder.AddFile(new FileEntry
                        {
                            RelativePath = file.RelativePath,
                            Hash = hash,
                            ChunkFile = chunkFileName,
                            FileSize = file.FileSize,
                            LastWriteTimeUtc = file.LastWriteTimeUtc,
                            CompressionSkipped = (group.GroupType == FileGroupType.NonCompressible || group.GroupType == FileGroupType.HugeNonCompressible),
                            CompressionRatio = 0
                        });
                        DateTimeOffset fileProcessEndUtc = DateTimeOffset.UtcNow;

                        reporter.ReportProgress(file.FileSize, file.RelativePath);
                        await recorder.RecordFileAsync(chunkEntryId, file.RelativePath, hash, file.FileSize, file.FileSize, file.LastWriteTimeUtc, fileProcessStartUtc, fileProcessEndUtc);
                    }
                    catch (Exception ex)
                    {
                        reporter.Warning($"Failed to add file '{file.SourcePath}': {ex.Message}");
                    }
                }
            }
            using (var scope = PerformanceTracker.TrackSection("Compress Chunk"))
            {
                scope.Set("chunkIndex", group.Index);
                scope.Set("chunkType", group.GroupType.ToString());
                var originalSize = new FileInfo(tempZipPath).Length;
                // Step 2: Apply outer compression (GZip, Brotli, or Zstd) unless group is marked as non-compressible
                if (group.GroupType == FileGroupType.NonCompressible || group.GroupType == FileGroupType.HugeNonCompressible)
                {
                    File.Copy(tempZipPath, outputPath, overwrite: true);
                }
                else
                {
                    using var zipInput = new FileStream(tempZipPath, FileMode.Open, FileAccess.Read);
                    using var outStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
                    compressor.Compress(zipInput, outStream);
                }

                chunkHash = HashHelper.ComputeHash(outputPath);
                hashManifestBuilder.AddChunk(new ChunkHashEntry
                {
                    ChunkFile = $"{group.Index:D4}.fgchunk",
                    Hash = chunkHash,
                    SizeBytes = new FileInfo(outputPath).Length
                });

                compressedSize = new FileInfo(outputPath).Length;
                var ratio = originalSize > 0
                    ? (1.0 - ((double)compressedSize / originalSize)) * 100.0
                    : 0;

                scope.Set("originalSize", originalSize);
                scope.Set("compressedSize", compressedSize);
                scope.Set("compressionRatio", ratio);
            }
            await recorder.CompleteChunkAsync(chunkEntryId, chunkHash, compressedSize, createTime, compressTime);
        }
        catch (Exception ex)
        {
            reporter.Error($"Failed to create chunk {group.Index}: {ex.Message}");
        }
        finally
        {
            // Step 3: Clean up temp file
            try { if (File.Exists(tempZipPath)) File.Delete(tempZipPath); } catch { }
        }
    }
}