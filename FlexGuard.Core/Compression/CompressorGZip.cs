using System.IO.Compression;

namespace FlexGuard.Core.Compression;

public class CompressorGZip : ICompressor
{
    public string FileExtension => ".gz";
    public void Compress(string inputFilePath, string outputFilePath)
    {
        using var inputFile = File.OpenRead(inputFilePath);
        using var outputFile = File.Create(outputFilePath);
        using var gzipStream = new GZipStream(outputFile, CompressionLevel.Optimal);
        inputFile.CopyTo(gzipStream);
    }
}