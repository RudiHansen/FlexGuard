using FlexGuard.Core.Backup;
using FlexGuard.Core.Compression;
using FlexGuard.Core.Hashing;
using FlexGuard.Core.Config;
using System.Text.Json;
using Spectre.Console;

class Program
{
    static void Main(string[] args)
    {
        const string configPath = "config.json";

        if (!File.Exists(configPath))
        {
            AnsiConsole.MarkupLine("[red]Configuration file not found: {0}[/]", configPath);
            return;
        }

        var configJson = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<BackupConfig>(configJson);

        if (config == null)
        {
            AnsiConsole.MarkupLine("[red]Failed to parse configuration file.[/]");
            return;
        }

        if (!Directory.Exists(config.DestinationPath))
        {
            AnsiConsole.MarkupLine($"[red]Destination root path not found: {config.DestinationPath}[/]");
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

        AnsiConsole.MarkupLine("[green]Backup completed successfully![/]");
    }
}