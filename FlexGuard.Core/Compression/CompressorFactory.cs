namespace FlexGuard.Core.Compression
{
    public static class CompressorFactory
    {
        public static ICompressor Create(CompressionMethod method)
        {
            return method switch
            {
                CompressionMethod.GZip => new CompressorGZip(),
                CompressionMethod.Brotli => new CompressorBrotli(),
                CompressionMethod.Zstd => new CompressorZstd(),
                _ => throw new NotSupportedException($"Compression method '{method}' is not supported.")
            };
        }
    }
}