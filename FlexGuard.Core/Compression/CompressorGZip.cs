using System.IO.Compression;

namespace FlexGuard.Core.Compression
{
    public class CompressorGZip : ICompressor
    {
        public string Name => "GZip";

        public void Compress(Stream input, Stream output)
        {
            using var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true);
            input.CopyTo(gzip);
        }

        public void Decompress(Stream input, Stream output)
        {
            using var gzip = new GZipStream(input, CompressionMode.Decompress, leaveOpen: true);
            gzip.CopyTo(output);
        }
    }
}