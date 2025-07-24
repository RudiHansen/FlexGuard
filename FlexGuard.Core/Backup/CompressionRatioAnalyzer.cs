using FlexGuard.Core.Manifest;
using System.IO.Compression;

namespace FlexGuard.Core.Backup;

public static class CompressionRatioAnalyzer
{
    public static double AnalyzeFile(PendingFileEntry file)
    {
        try
        {
            using var fileStream = new FileStream(file.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var zipStream = new GZipStream(fileStream, CompressionMode.Decompress);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            var entry = archive.GetEntry(file.RelativePath);
            if (entry == null)
                return 0;

            long compressedSize = entry.CompressedLength;

            return file.FileSize > 0
                ? Math.Round(100.0 * (file.FileSize - compressedSize) / file.FileSize, 2)
                : 0;
        }
        catch
        {
            // If the chunk or entry is invalid or corrupted, return 0
            return 0;
        }
    }
}
