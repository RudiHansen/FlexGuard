using ZstdSharp;

namespace FlexGuard.Core.Compression
{
    public class CompressorZstd : ICompressor
    {
        public string Name => "Zstd";

        public void Compress(Stream input, Stream output)
        {
            using var zstdStream = new ZstdSharp.CompressionStream(output);
            input.CopyTo(zstdStream);
        }

        public void Decompress(Stream input, Stream output)
        {
            using var zstdStream = new ZstdSharp.DecompressionStream(input);
            zstdStream.CopyTo(output);
        }
    }
}
