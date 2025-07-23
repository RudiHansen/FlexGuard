namespace FlexGuard.Core.Config;

public class BackupJobConfig
{
    public required string JobName { get; set; }
    public required List<BackupSource> Sources { get; set; } = new();
    public required string DestinationPath { get; set; } = string.Empty;
    public string? RestoreTargetFolder { get; set; }
}
public class BackupSource
{
    public required string Path { get; set; } = string.Empty;
    public List<string> Exclude { get; set; } = new();
}