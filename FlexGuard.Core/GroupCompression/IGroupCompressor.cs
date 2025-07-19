namespace FlexGuard.Core.GroupCompression;

/// <summary>
/// Compresses a group of files into a single archive file.
/// </summary>
public interface IGroupCompressor
{
    /// <param name="files">List of full file paths to include in the archive.</param>
    /// <param name="outputFilePath">Path to the output archive file (e.g., .zip or .fgz).</param>
    /// <param name="rootPath">Base path to calculate relative file paths from.</param>
    /// <returns>A list of compressed file metadata used for manifest and restore.</returns>
    List<GroupCompressedFile> CompressFiles(IEnumerable<string> files, string outputFilePath, string rootPath);
}