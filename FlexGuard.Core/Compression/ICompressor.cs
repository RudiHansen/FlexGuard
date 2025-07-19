namespace FlexGuard.Core.Compression;

public interface ICompressor
{
    void Compress(string inputFilePath, string outputFilePath);
    string FileExtension { get; }
}