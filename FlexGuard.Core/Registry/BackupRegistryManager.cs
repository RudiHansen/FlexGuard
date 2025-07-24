using FlexGuard.Core.Options;
using FlexGuard.Core.Util;
using System.Text.Json;

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

    public BackupRegistry.BackupEntry AddEntry(DateTime timestamp, OperationMode mode)
    {
        var entry = new BackupRegistry.BackupEntry
        {
            TimestampStart = timestamp,
            Type = mode
        };

        _registry.Backups.Add(entry);
        return entry;
    }

    public void Save()
    {
        var directory = Path.GetDirectoryName(_registryPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory); // Sørger for at mappen findes
        }

        var json = JsonSerializer.Serialize(_registry, JsonSettings.Indented);
        File.WriteAllText(_registryPath, json);
    }
    public BackupRegistry.BackupEntry? GetLatestEntry(OperationMode _type)
    {
        return _registry.Backups
            .OrderByDescending(e => e.TimestampStart)
            .Where(e => e.Type == _type)
            .FirstOrDefault();
    }
    public BackupRegistry GetRegistry()
    {
        return _registry;
    }
}