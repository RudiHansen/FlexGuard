using FlexGuard.Core.Compression;
using FlexGuard.Core.Options;

namespace FlexGuard.CLI.Options;

public class ProgramOptionsBuilder
{
    public string JobName { get; set; } = "DefaultJob";
    public OperationMode Mode { get; set; } = OperationMode.FullBackup;
    public int MaxFilesPerGroup { get; set; } = 0;
    public long MaxBytesPerGroup { get; set; } = 1024 * 1024 * 1024; // 1 GB
    public int MaxParallelTasks { get; set; } = 8;
    public bool EnableCompressionRatioMeasurement { get; set; } = false;
    public CompressionMethod Compression { get; set; } = CompressionMethod.Zstd;
    public string ImportBackupDataPath { get; set; } = "";

    public ProgramOptions Build()
    {
        var maxParallel = Math.Max(1, MaxParallelTasks); // Ensure MaxParallelTasks is min 1

        return new ProgramOptions(
            JobName,
            Mode,
            MaxFilesPerGroup,
            MaxBytesPerGroup,
            maxParallel,
            EnableCompressionRatioMeasurement,
            Compression,
            ImportBackupDataPath
        );
    }
}
