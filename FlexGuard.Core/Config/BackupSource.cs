namespace FlexGuard.Core.Config;

public class BackupSource
{
    public string Path { get; set; } = string.Empty;
    public List<string> Exclude { get; set; } = new();
}