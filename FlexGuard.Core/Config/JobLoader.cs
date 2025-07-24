using FlexGuard.Core.Util;
using System.Text.Json;

namespace FlexGuard.Core.Config;

public static class JobLoader
{
    public static BackupJobConfig Load(string jobName)
    {
        var baseDir = Path.Combine(AppContext.BaseDirectory, "Jobs");
        var filePath = Path.Combine(baseDir, $"job_{jobName}.json");

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Job config file not found: {filePath}");

        var json = File.ReadAllText(filePath);

        var config = JsonSerializer.Deserialize<BackupJobConfig>(json, JsonSettings.DeserializeIgnoreCase);

        return config ?? throw new InvalidOperationException($"Could not parse job config file: {filePath}");
    }
}