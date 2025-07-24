using System.IO.Compression;

namespace FlexGuard.Core.Compression
{
    public class CompressorBrotli : ICompressor
    {
        public string Name => "Brotli";

        public void Compress(Stream input, Stream output)
        {
            using var brotli = new BrotliStream(output, CompressionLevel.Optimal, leaveOpen: true);
            input.CopyTo(brotli);
        }

        public void Decompress(Stream input, Stream output)
        {
            using var brotli = new BrotliStream(input, CompressionMode.Decompress, leaveOpen: true);
            brotli.CopyTo(output);
        }
    }
}