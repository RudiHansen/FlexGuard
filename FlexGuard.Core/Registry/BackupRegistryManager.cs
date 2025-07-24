using System.Text.Json;
using FlexGuard.Core.Options;

namespace FlexGuard.Core.Registry;

public class BackupRegistryManager
{
    private readonly string _registryPath;
    private BackupRegistry _registry;

    public BackupRegistryManager(string jobName, string jobFolder)
    {
        _registryPath = Path.Combine(jobFolder, $"registry_{jobName}.json");

        if (File.Exists(_registryPath))
        {
            var json = File.ReadAllText(_registryPath);
            _registry = JsonSerializer.Deserialize<BackupRegistry>(json) ?? new BackupRegistry { JobName = jobName };
        }
        else
        {
            _registry = new BackupRegistry { JobName = jobName };
        }
    }

    public IReadOnlyList<BackupRegistry.BackupEntry> Entries => _registry.Backups;

    public void AddEntry(DateTime timestamp, OperationMode mode, string manifestFileName, string destinationFolderName)
    {
        _registry.Backups.Add(new BackupRegistry.BackupEntry
        {
            Timestamp = timestamp,
            Type = mode.ToString(),
            ManifestFileName = manifestFileName,
            DestinationFolderName = Path.GetFileName(destinationFolderName)
        });
    }

    public void Save()
    {
        var directory = Path.GetDirectoryName(_registryPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory); // Sørger for at mappen findes
        }

        var json = JsonSerializer.Serialize(_registry, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_registryPath, json);
    }
    public BackupRegistry.BackupEntry? GetLatestEntry(string _type)
    {
        return _registry.Backups
            .OrderByDescending(e => e.Timestamp)
            .Where(e => e.Type.Equals(_type, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();
    }
}