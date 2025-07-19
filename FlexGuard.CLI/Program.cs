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

        var compressor = new GZipCompressor();
        var hasher = new Sha256Hasher();
        var strategy = new FullBackupStrategy(compressor, hasher);

        AnsiConsole.Status()
            .Start("Running backup...", ctx =>
            {
                strategy.RunBackup(config);
                ctx.Status("Backup complete");
            });

        AnsiConsole.MarkupLine("[green]Backup completed successfully![/]");
    }
}