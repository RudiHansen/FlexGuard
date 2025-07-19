using FlexGuard.CLI.Util;
using FlexGuard.Core.Backup;
using FlexGuard.Core.Compression;
using FlexGuard.Core.Config;
using FlexGuard.Core.Hashing;
using Spectre.Console;
using System.Text.Json;

class Program
{
    static void Main(string[] args)
    {
        const string configPath = "config.json";

        OutputHelper.Init(debugToConsole: true, debugToFile: true);
        OutputHelper.Info("Starting FlexGuard backup...");

        if (!File.Exists(configPath))
        {
            OutputHelper.Error($"Configuration file not found: {configPath}");
            return;
        }

        var configJson = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<BackupConfig>(configJson);

        if (config == null)
        {
            OutputHelper.Error("Failed to parse configuration file.");
            return;
        }

        if (!Directory.Exists(config.DestinationPath))
        {
            OutputHelper.Error($"Destination root path not found: {config.DestinationPath}");
            return;
        }

        // Tilføj backup-type og tidspunkt
        string backupType = "Full";
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HHmm");
        string subfolderName = $"{timestamp}_{backupType}";
        string fullDestPath = Path.Combine(config.DestinationPath, subfolderName);
        Directory.CreateDirectory(fullDestPath);

        var compressor = new GZipCompressor();
        var hasher = new Sha256Hasher();
        var strategy = new FullBackupStrategy(compressor, hasher);

        AnsiConsole.Status()
            .Start("Running backup...", ctx =>
            {
                strategy.RunBackup(config, fullDestPath);
                ctx.Status("Backup complete");
            });

        OutputHelper.Success("Backup completed successfully!");
    }
}