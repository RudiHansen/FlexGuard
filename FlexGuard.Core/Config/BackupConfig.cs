namespace FlexGuard.Core.Config;

public class BackupConfig
{
    public List<BackupSource> Sources { get; set; } = new();
    public string DestinationPath { get; set; } = string.Empty;
}