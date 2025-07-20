using FlexGuard.Core.Backup;
using FlexGuard.Core.Compression;
using FlexGuard.Core.Config;
using FlexGuard.Core.GroupCompression;
using FlexGuard.Core.Hashing;
using FlexGuard.Core.Reporting;
using Spectre.Console;
using System.Text.Json;

class Program
{
    static void Main(string[] args)
    {
        const string configPath = "config.json";

        var reporter = new MessageReporterConsole(debugToConsole: true, debugToFile: true);
        reporter.Info("Starting FlexGuard backup...");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        if (!File.Exists(configPath))
        {
            reporter.Error($"Configuration file not found: {configPath}");
            return;
        }

        var configJson = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<BackupConfig>(configJson);

        if (config == null)
        {
            reporter.Error("Failed to parse configuration file.");
            return;
        }

        if (!Directory.Exists(config.DestinationPath))
        {
            reporter.Error($"Destination root path not found: {config.DestinationPath}");
            return;
        }

        // Add timestamp and backup type
        string backupType = "Full";
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HHmm");
        string subfolderName = $"{timestamp}_{backupType}";
        string fullDestPath = Path.Combine(config.DestinationPath, subfolderName);
        Directory.CreateDirectory(fullDestPath);

        var compressor = new CompressorGZip();
        var hasher = new Sha256Hasher();
        var groupCompressor = new GroupCompressorZip(hasher);
        long maxBytesPerGroup = 100 * 1024 * 1024;

        AnsiConsole.Progress().Start(ctx =>
        {
            var task = ctx.AddTask("[green]Backing up files[/]");

            var progressReporter = new MessageReporterWithProgress(reporter, (current, total, file) =>
            {
                if (task.MaxValue != total)
                    task.MaxValue = total;

                task.Value = current;
                task.Description = $"[green]Backing up: {Path.GetFileName(file)}[/]";
            });

            var backupProcessor = new BackupProcessorGroupFile(
                groupCompressor,
                maxFilesPerGroup: 100,
                maxBytesPerGroup: maxBytesPerGroup,
                reporter: progressReporter);

            var strategy = new BackupStrategyFull(backupProcessor, progressReporter);
            strategy.RunBackup(config, fullDestPath, progressReporter);
        });

        stopwatch.Stop();
        reporter.Success("Backup completed successfully!");
        reporter.Info($"Backup duration: {stopwatch.Elapsed:hh\\:mm\\:ss}");
    }
}