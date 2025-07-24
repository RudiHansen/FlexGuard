using ZstdSharp;

namespace FlexGuard.Core.Compression
{
    public class CompressorZstd : ICompressor
    {
        public string Name => "Zstd";

        public void Compress(Stream input, Stream output)
        {
            using var compressor = new Compressor(level: 3); // 1=fast, 22=best compression
            using var ms = new MemoryStream();
            input.CopyTo(ms);

            byte[] inputBytes = ms.ToArray();
            byte[] compressed = compressor.Wrap(inputBytes).ToArray(); // Konverter Span<byte> til byte[]

            output.Write(compressed, 0, compressed.Length);
        }

        public void Decompress(Stream input, Stream output)
        {
            using var decompressor = new Decompressor();
            using var ms = new MemoryStream();
            input.CopyTo(ms);

            byte[] compressedBytes = ms.ToArray();
            byte[] decompressed = decompressor.Unwrap(compressedBytes).ToArray(); // Konverter Span<byte> til byte[]

            output.Write(decompressed, 0, decompressed.Length);
        }
    }
}
