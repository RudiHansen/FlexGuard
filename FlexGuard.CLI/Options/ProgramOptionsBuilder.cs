using FlexGuard.Core.Compression;
using FlexGuard.Core.Options;

namespace FlexGuard.CLI.Options;

public class ProgramOptionsBuilder
{
    public string JobName { get; set; } = "DefaultJob";
    public OperationMode Mode { get; set; } = OperationMode.FullBackup;
    public int MaxFilesPerGroup { get; set; } = 1000;
    public long MaxBytesPerGroup { get; set; } = 1024 * 1024 * 1024; // 1 GB
    public bool EnableCompressionRatioMeasurement { get; set; } = false;
    public CompressionMethod Compression { get; set; } = CompressionMethod.Zstd;

    public ProgramOptions Build()
    {
        return new ProgramOptions(
            JobName,
            Mode,
            MaxFilesPerGroup,
            MaxBytesPerGroup,
            EnableCompressionRatioMeasurement,
            Compression
        );
    }
}
