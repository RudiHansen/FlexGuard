using FlexGuard.Core.Config;
using FlexGuard.Core.Reporting;
using Renci.SshNet;

namespace FlexGuard.Core.Backup;

public sealed class RemoteSshFileStore
{
    private readonly BackupJobConfig _jobConfig;
    private readonly IMessageReporter _reporter;

    public RemoteSshFileStore(BackupJobConfig jobConfig, IMessageReporter reporter)
    {
        _jobConfig = jobConfig ?? throw new ArgumentNullException(nameof(jobConfig));
        _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
    }

    public async Task UploadBackupFolderAsync(string localFolder)
    {
        if (string.IsNullOrWhiteSpace(localFolder))
            throw new ArgumentException("Local folder is not specified.", nameof(localFolder));

        if (!Directory.Exists(localFolder))
            throw new DirectoryNotFoundException($"Local backup folder not found: {localFolder}");

        var remote = _jobConfig.Remote;

        if (remote is null || !remote.Enabled)
        {
            _reporter.Info("Remote upload is disabled or not configured for this job.");
            return;
        }

        if (string.IsNullOrWhiteSpace(remote.Host))
            throw new InvalidOperationException("Remote.Host is not configured.");

        if (string.IsNullOrWhiteSpace(remote.Username))
            throw new InvalidOperationException("Remote.Username is not configured.");

        if (string.IsNullOrWhiteSpace(remote.PrivateKeyPath))
              throw new InvalidOperationException("Remote.PrivateKeyPath must be configured.");

        // Udled runFolderName fra localFolder
        var runFolderName = Path.GetFileName(
            localFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        var remotePath = remote.RemotePath!
            .Replace("{JobName}", _jobConfig.JobName)
            .Replace("{RunFolder}", runFolderName);

        _reporter.Info($"Connecting to remote SSH server {remote.Host}:{remote.Port} ...");

        using var client = new SftpClient(
            remote.Host,
            remote.Port,
            remote.Username,
            new PrivateKeyFile(remote.PrivateKeyPath));

        client.Connect();
        _reporter.Success("Connected.");

        EnsureRemoteDirectory(client, remotePath);

        var files = Directory.GetFiles(localFolder);
        _reporter.Info($"Uploading {files.Length} file(s) to {remotePath} ...");

        foreach (var filePath in files)
        {
            var fileName = Path.GetFileName(filePath);
            var remoteFileFullPath = $"{remotePath}/{fileName}";
            var fileSize = new FileInfo(filePath).Length;

            using var fs = File.OpenRead(filePath);

            _reporter.Info($"Uploading {fileName} ({fileSize} bytes) ...");
            client.UploadFile(fs, remoteFileFullPath, true);

            var uploadedSize = client.GetAttributes(remoteFileFullPath).Size;
            if (uploadedSize != fileSize)
            {
                throw new InvalidOperationException(
                    $"Size mismatch for file: {fileName}. Local={fileSize}, Remote={uploadedSize}");
            }

            _reporter.Success($"Uploaded {fileName}");
        }

        client.Disconnect();
        _reporter.Success("Remote upload completed successfully.");

        await Task.CompletedTask;
    }


    private static void EnsureRemoteDirectory(SftpClient client, string remotePath)
    {
        var parts = remotePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = "/";

        foreach (var part in parts)
        {
            current = current.EndsWith('/')
                ? current + part
                : current + "/" + part;

            if (!client.Exists(current))
            {
                client.CreateDirectory(current);
            }
        }
    }
}