using System.IO.Compression;
using FlexGuard.Core.Hashing;

namespace FlexGuard.Core.GroupCompression;

public class GroupCompressorZip : IGroupCompressor
{
    private readonly IHasher _hasher;

    public GroupCompressorZip(IHasher hasher)
    {
        _hasher = hasher;
    }

    public List<GroupCompressedFile> CompressFiles(IEnumerable<string> files, string outputFilePath, string rootPath, Action<string>? reportFileProcessed = null)
    {
        var result = new List<GroupCompressedFile>();

        using var zipFile = File.Create(outputFilePath);
        using var archive = new ZipArchive(zipFile, ZipArchiveMode.Create, leaveOpen: false);

        foreach (var file in files)
        {
            reportFileProcessed?.Invoke(file);
            string relativePath = Path.GetRelativePath(rootPath, file);
            string hash = _hasher.ComputeHash(file);

            archive.CreateEntryFromFile(file, relativePath, CompressionLevel.Optimal);

            result.Add(new GroupCompressedFile
            {
                SourcePath = file,
                RelativePath = relativePath,
                Hash = hash
            });
        }

        return result;
    }
}