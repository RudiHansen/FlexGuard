using FlexGuard.Core.Compression;

namespace FlexGuard.Core.Options;

public class ProgramOptions
{
    public string JobName { get; }
    public OperationMode Mode { get; }

    public int MaxFilesPerGroup { get; init; } = 1000;
    public long MaxBytesPerGroup { get; init; } = 1024 * 1024 * 1024; // 1 Gb
    public int MaxParallelTasks { get; init; } = 8;
    public bool EnableCompressionRatioMeasurement { get; init; } = false;
    public CompressionMethod Compression { get; set; } = CompressionMethod.Zstd;

    public ProgramOptions(string jobName, OperationMode mode)
    {
        JobName = jobName;
        Mode = mode;
    }
    public ProgramOptions(string jobName,OperationMode mode,int maxFilesPerGroup,long maxBytesPerGroup, int maxParallelTasks, bool enableCompressionRatioMeasurement,CompressionMethod compression)
    {
        JobName = jobName ?? throw new ArgumentNullException(nameof(jobName));
        Mode = mode;
        MaxFilesPerGroup = maxFilesPerGroup;
        MaxBytesPerGroup = maxBytesPerGroup;
        MaxParallelTasks = maxParallelTasks;
        MaxParallelTasks = maxParallelTasks;
        EnableCompressionRatioMeasurement = enableCompressionRatioMeasurement;
        Compression = compression;
    }
}
public enum OperationMode
{
    FullBackup,
    DifferentialBackup,
    Restore
}