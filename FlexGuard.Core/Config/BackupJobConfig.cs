namespace FlexGuard.Core.Config;

public class BackupJobConfig
{
    public required string JobName { get; set; }
    public required List<BackupSource> Sources { get; set; } = new();
    public required string DestinationPath { get; set; } = string.Empty;
    public required string RestoreTargetFolder { get; set; }
    public RemoteSsh? Remote { get; set; } = new();

}
public class BackupSource
{
    public required string Path { get; set; } = string.Empty;
    public List<string> Exclude { get; set; } = new();
}
public class RemoteSsh
{
    public bool Enabled { get; set; }
    public string? Host { get; set; }
    public int Port { get; set; }
    public string? Username { get; set; }
    public string? PrivateKeyPath { get; set; }
    public string? RemotePath { get; set; }
}