using FlexGuard.Core.Model;
using FlexGuard.Core.Options;
using FlexGuard.Core.Reporting;
using System.IO.Compression;

public static class ChunkProcessor
{
    public static void Process(FileGroup group, string backupFolderPath, ProgramOptions options, IMessageReporter reporter)
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
                    var entry = archive.CreateEntry(file.RelativePath, CompressionLevel.Optimal);

                    using var entryStream = entry.Open();
                    using var sourceStream = new FileStream(file.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                    sourceStream.CopyTo(entryStream);
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