namespace FlexGuard.Core.Options;

public class ProgramOptions
{
    public string JobName { get; }
    public OperationMode Mode { get; }
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

