using System.Security.Cryptography;
using System.IO.Compression;
using FlexGuard.Core.Compression;
using FlexGuard.Core.Manifest;
using FlexGuard.Core.Options;
using FlexGuard.Core.Recording;
using FlexGuard.Core.Reporting;
using FlexGuard.Core.Util;

namespace FlexGuard.Core.Backup;

public static class ChunkProcessor
{
    public static async Task ProcessAsync(
        FileGroup group,
        string backupFolderPath,
        IMessageReporter reporter,
        ProgramOptions options,
        BackupRunRecorder recorder)
    {
        var chunkFileName = $"{group.Index:D4}.fgchunk";
        var outputPath = Path.Combine(backupFolderPath, chunkFileName);

        Directory.CreateDirectory(backupFolderPath);

        var zipCompressionLevel = CompressionLevel.NoCompression;
        var compressor = CompressorFactory.Create(options.Compression);

        var tempZipPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        string chunkHash;
        long compressedSize;
        long originalSize;
        TimeSpan createTime;
        TimeSpan compressTime;

        try
        {
            // start chunk i recorder (som før)
            string chunkEntryId = await recorder.StartChunkAsync(chunkFileName, options.Compression, group.Index);

            using var meterChunk = ResourceUsageMeter.Start();
            CompressionMethod actualChunkCompressionMethod = options.Compression;
            TimeSpan timerChunkCreateElapsed;

            // -----------------------------------------------------------
            // 1) LAV ZIP-FILEN (nu med async I/O og hash i ét gennemløb)
            // -----------------------------------------------------------
            {
                using var timerChunkCreate = TimingScope.Start();

                // temp zip-fil med async flag og stor buffer
                await using var zipFileStream = new FileStream(
                    tempZipPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 1_048_576,
                    options: FileOptions.Asynchronous);

                using var archive = new ZipArchive(zipFileStream, ZipArchiveMode.Create, leaveOpen: false);

                foreach (var file in group.Files)
                {
                    try
                    {
                        DateTimeOffset fileProcessStartUtc = DateTimeOffset.UtcNow;

                        using var timerFileCreate = TimingScope.Start();
                        using var meterFile = ResourceUsageMeter.Start();

                        // kildefil åbnes async
                        await using var sourceStream = new FileStream(
                            file.SourcePath,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.Read,
                            bufferSize: 1_048_576,
                            options: FileOptions.Asynchronous);

                        // vi laver entry som før
                        var entry = archive.CreateEntry(file.RelativePath, zipCompressionLevel);
                        // entryStream er ikke IAsyncDisposable, så almindelig using
                        using var entryStream = entry.Open();

                        // hash i ét gennemløb mens vi skriver til zip
                        using var sha256 = SHA256.Create();
                        byte[] buffer = new byte[1_048_576];
                        int read;
                        long written = 0;

                        while ((read = await sourceStream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
                        {
                            // skriv til zip
                            await entryStream.WriteAsync(buffer.AsMemory(0, read));
                            // pump til hash
                            sha256.TransformBlock(buffer, 0, read, null, 0);
                            written += read;
                        }

                        sha256.TransformFinalBlock([], 0, 0);
                        string hash = Convert.ToHexString(sha256.Hash!).ToLowerInvariant();

                        DateTimeOffset fileProcessEndUtc = DateTimeOffset.UtcNow;

                        reporter.ReportProgress(file.FileSize, file.RelativePath);

                        var meterFileResult = meterFile.Stop();
                        timerFileCreate.Stop();

                        // recorder-kaldet er det samme som før
                        await recorder.RecordFileAsync(
                            chunkEntryId,
                            file.RelativePath,
                            hash,
                            CompressionMethod.None,
                            file.FileSize,
                            file.FileSize,
                            file.LastWriteTimeUtc,
                            fileProcessStartUtc,
                            fileProcessEndUtc,
                            timerFileCreate.Elapsed,
                            meterFileResult.CpuTime,
                            meterFileResult.PeakCpuPercent,
                            meterFileResult.PeakWorkingSetBytes,
                            meterFileResult.PeakManagedBytes);
                    }
                    catch (FileNotFoundException)
                    {
                        reporter.Warning($"Skipped missing file '{file.SourcePath}' (file disappeared during backup).");
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        reporter.Warning($"Failed to add file '{file.SourcePath}': access denied ({ex.Message}).");
                    }
                    catch (IOException ex)
                    {
                        reporter.Warning($"I/O error while adding file '{file.SourcePath}': {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        reporter.Error($"Failed to add file '{file.SourcePath}': {ex.Message} : {ex.StackTrace}");
                    }
                }

                timerChunkCreate.Stop();
                timerChunkCreateElapsed = timerChunkCreate.Elapsed;
            }

            createTime = timerChunkCreateElapsed;

            // -----------------------------------------------------------
            // 2) YDRE KOMPRIMERING (samme logik, men vi åbner streams async
            //    og lægger selve den tunge Compress i Task.Run, så vi ikke
            //    blokerer hele async-kæden)
            // -----------------------------------------------------------
            using var timerChunkCompress = TimingScope.Start();

            originalSize = new FileInfo(tempZipPath).Length;

            if (group.GroupType == FileGroupType.NonCompressible || group.GroupType == FileGroupType.HugeNonCompressible)
            {
                // bare kopi – det er fint sync
                File.Copy(tempZipPath, outputPath, overwrite: true);
                actualChunkCompressionMethod = CompressionMethod.None;
            }
            else
            {
                await using var zipInput = new FileStream(
                    tempZipPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 1_048_576,
                    options: FileOptions.Asynchronous);

                await using var outStream = new FileStream(
                    outputPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 1_048_576,
                    options: FileOptions.Asynchronous);

                // selve compress er stadig sync i dine compressor-klasser,
                // så vi kører den på threadpool for ikke at blokere caller
                await Task.Run(() =>
                {
                    compressor.Compress(zipInput, outStream);
                });
                actualChunkCompressionMethod = options.Compression;
            }

            // hash på færdigt chunk som før
            chunkHash = HashHelper.ComputeHash(outputPath);
            compressedSize = new FileInfo(outputPath).Length;

            timerChunkCompress.Stop();
            compressTime = timerChunkCompress.Elapsed;

            var result = meterChunk.Stop();

            // recorder-complete som før
            await recorder.CompleteChunkAsync(
                chunkEntryId,
                chunkHash,
                actualChunkCompressionMethod,
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
            try
            {
                if (File.Exists(tempZipPath))
                    File.Delete(tempZipPath);
            }
            catch
            {
                // ignore cleanup errors
            }
        }
    }
}