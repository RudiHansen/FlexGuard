using System.IO.Compression;

namespace FlexGuard.Core.Compression;

public class DeflateCompressor : ICompressor
{
    public string FileExtension => ".def";
    public void Compress(string inputFilePath, string outputFilePath)
    {
        using var inputFile = File.OpenRead(inputFilePath);
        using var outputFile = File.Create(outputFilePath);
        using var deflateStream = new DeflateStream(outputFile, CompressionLevel.Optimal);
        inputFile.CopyTo(deflateStream);
    }
}