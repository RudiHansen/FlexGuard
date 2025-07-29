using FlexGuard.Core.Compression;
using FlexGuard.Core.Reporting;
using FlexGuard.Core.Util;
using System.IO.Compression;

namespace FlexGuard.Core.Restore;

public static class RestoreHelper
{
    public static void RestoreFile(
        string restoreTargetFolder,
        string chunkFilePath,
        string chunkHash,
        string relativePath,
        long fileSize,
        string expectedHash,
        CompressionMethod compressionMethod,
        IMessageReporter reporter)
    {
        if (string.IsNullOrWhiteSpace(restoreTargetFolder))
        {
            reporter.Error("Restore target folder is not defined.");
            return;
        }

        if (!File.Exists(chunkFilePath))
        {
            reporter.Error($"Chunk file not found: {chunkFilePath}");
            return;
        }
        if (!string.IsNullOrEmpty(chunkHash))
        {
            var actualHash = HashHelper.ComputeHash(chunkFilePath);
            if (!chunkHash.Equals(actualHash, StringComparison.OrdinalIgnoreCase))
            {
                reporter.Error($"Chunk hash mismatch: {chunkFilePath}");
                return;
            }
            reporter.Debug($"Chunk hash verified: {chunkFilePath}");
        }

        try
        {
            // Create the correct decompressor
            var compressor = CompressorFactory.Create(compressionMethod);

            // Open chunk and decompress to memory
            using var chunkStream = new FileStream(chunkFilePath, FileMode.Open, FileAccess.Read);
            using var decompressedStream = new MemoryStream();
            compressor.Decompress(chunkStream, decompressedStream);
            decompressedStream.Position = 0;

            using var zipArchive = new ZipArchive(decompressedStream, ZipArchiveMode.Read);

            var zipEntry = zipArchive.GetEntry(relativePath);
            if (zipEntry == null)
            {
                reporter.Error($"Entry '{relativePath}' not found in archive.");
                return;
            }

            // Prepare restore path
            var outputPath = Path.GetFullPath(Path.Combine(restoreTargetFolder, relativePath));
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir))
                Directory.CreateDirectory(outputDir);

            // Write file
            using (var entryStream = zipEntry.Open())
            using (var outputFile = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
            {
                entryStream.CopyTo(outputFile);
            }

            // Verify hash
            var actualHash = HashHelper.ComputeHash(outputPath);
            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                reporter.Warning($"Hash mismatch for '{relativePath}'. Expected: {expectedHash}, Actual: {actualHash}");
            }
            else
            {
                reporter.ReportProgress(fileSize, relativePath);
            }
        }
        catch (Exception ex)
        {
            reporter.Error($"Restore failed for '{relativePath}': {ex.Message}");
        }
    }
}