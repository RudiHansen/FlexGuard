using FlexGuard.Core.Compression;
using FlexGuard.Core.Manifest;
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

        // Determine ZIP compression level for the inner archive (no compression inside zip)
        var zipCompressionLevel = CompressionLevel.NoCompression;

        // Select outer compressor based on the manifest (GZip, Brotli, Zstd)
        var compressor = CompressorFactory.Create(fileManifestBuilder.Compression);

        // Step 1: Create temporary zip file path
        var tempZipPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        string chunkHash;
        long compressedSize;
        long originalSize;
        TimeSpan createTime;
        TimeSpan compressTime;

        try
        {
            string chunkEntryId = await recorder.StartChunkAsync(chunkFileName, fileManifestBuilder.Compression, group.Index);

            using var meterChunk = ResourceUsageMeter.Start();
            TimeSpan timerChunkCreateElapsed;

            // ---- Inner scope: create and write the zip archive (ensure dispose before reading it) ----
            {
                using var timerChunkCreate = TimingScope.Start();

                // Create temp zip and write entries
                using var zipFileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var archive = new ZipArchive(zipFileStream, ZipArchiveMode.Create, leaveOpen: false);

                foreach (var file in group.Files)
                {
                    try
                    {
                        DateTimeOffset fileProcessStartUtc = DateTimeOffset.UtcNow;

                        using var timerFileCreate = TimingScope.Start();
                        using var meterFile = ResourceUsageMeter.Start();

                        using var sourceStream = new FileStream(file.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                        // Compute hash directly from stream, then reset position before copy
                        string hash = HashHelper.ComputeHash(sourceStream);
                        sourceStream.Position = 0; // IMPORTANT: reset after hashing

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

                        var meterFileResult = meterFile.Stop();
                        timerFileCreate.Stop();

                        await recorder.RecordFileAsync(
                            chunkEntryId,
                            file.RelativePath,
                            hash,
                            file.FileSize,
                            file.FileSize, // (inner zip is uncompressed; outer compression happens afterward)
                            file.LastWriteTimeUtc,
                            fileProcessStartUtc,
                            fileProcessEndUtc,
                            timerFileCreate.Elapsed,
                            meterFileResult.CpuTime,
                            meterFileResult.PeakCpuPercent,
                            meterFileResult.PeakWorkingSetBytes,
                            meterFileResult.PeakManagedBytes);
                    }
                    catch (Exception ex)
                    {
                        reporter.Warning($"Failed to add file '{file.SourcePath}': {ex.Message}");
                    }
                }

                timerChunkCreate.Stop();
                timerChunkCreateElapsed = timerChunkCreate.Elapsed;
            }
            // ---- end inner scope; archive/zipFileStream are disposed here ----

            createTime = timerChunkCreateElapsed;

            // Step 2: Apply outer compression (GZip, Brotli, or Zstd) unless group is marked as non-compressible
            using var timerChunkCompress = TimingScope.Start();

            originalSize = new FileInfo(tempZipPath).Length;

            if (group.GroupType == FileGroupType.NonCompressible || group.GroupType == FileGroupType.HugeNonCompressible)
            {
                File.Copy(tempZipPath, outputPath, overwrite: true);
            }
            else
            {
                using var zipInput = new FileStream(tempZipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var outStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
                compressor.Compress(zipInput, outStream);
            }

            chunkHash = HashHelper.ComputeHash(outputPath);
            hashManifestBuilder.AddChunk(new ChunkHashEntry
            {
                ChunkFile = chunkFileName,
                Hash = chunkHash,
                SizeBytes = new FileInfo(outputPath).Length
            });

            compressedSize = new FileInfo(outputPath).Length;

            timerChunkCompress.Stop();
            compressTime = timerChunkCompress.Elapsed;

            var result = meterChunk.Stop();

            await recorder.CompleteChunkAsync(
                chunkEntryId,
                chunkHash,
                originalSize,
                compressedSize,
                createTime,
                compressTime,
                result.CpuTime,
                result.PeakCpuPercent,
                result.PeakWorkingSetBytes,
                result.PeakManagedBytes);
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