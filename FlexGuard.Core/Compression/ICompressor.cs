namespace FlexGuard.Core.Compression
{
    public interface ICompressor
    {
        string Name { get; }

        /// <summary>
        /// Compresses the data from the input stream into the output stream.
        /// </summary>
        void Compress(Stream input, Stream output);

        /// <summary>
        /// Decompresses the data from the input stream into the output stream.
        /// </summary>
        void Decompress(Stream input, Stream output);
    }
}