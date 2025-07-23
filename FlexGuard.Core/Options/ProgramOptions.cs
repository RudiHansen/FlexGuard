namespace FlexGuard.Core.Options;

public class ProgramOptions
{
    public string JobName { get; }
    public OperationMode Mode { get; }

    public int MaxFilesPerGroup { get; init; } = 1000;
    public long MaxBytesPerGroup { get; init; } = 1024 * 1024 * 1024; // 1 Gb
    public bool EnableCompressionRatioMeasurement { get; init; } = false;

    public ProgramOptions(string jobName, OperationMode mode)
    {
        JobName = jobName;
        Mode = mode;
    }
}

public enum OperationMode
{
    FullBackup,
    DifferentialBackup,
    Restore
}