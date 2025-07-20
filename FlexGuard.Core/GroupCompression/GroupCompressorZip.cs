using System.IO.Compression;
using FlexGuard.Core.Hashing;
using FlexGuard.Core.Reporting;

namespace FlexGuard.Core.GroupCompression;

public class GroupCompressorZip : IGroupCompressor
{
    private readonly IHasher _hasher;
    private readonly IMessageReporter? _reporter;

    public GroupCompressorZip(IHasher hasher, IMessageReporter? reporter = null)
    {
        _hasher = hasher;
        _reporter = reporter;
    }

    public List<GroupCompressedFile> CompressFiles(
        IEnumerable<string> files,
        string outputFilePath,
        string rootPath,
        Action<string>? reportFileProcessed = null)
    {
        var result = new List<GroupCompressedFile>();

        using var zipFile = File.Create(outputFilePath);
        using var archive = new ZipArchive(zipFile, ZipArchiveMode.Create, leaveOpen: false);

        foreach (var file in files)
        {
            try
            {
                string relativePath = Path.GetRelativePath(rootPath, file);
                string hash = _hasher.ComputeHash(file); // May throw

                archive.CreateEntryFromFile(file, relativePath, CompressionLevel.Optimal); // May throw

                result.Add(new GroupCompressedFile
                {
                    SourcePath = file,
                    RelativePath = relativePath,
                    Hash = hash
                });

                reportFileProcessed?.Invoke(file);
            }
            catch (Exception ex)
            {
                _reporter?.Warning($"Skipped file '{file}': {ex.Message}");
                continue;
            }
        }

        return result;
    }
}