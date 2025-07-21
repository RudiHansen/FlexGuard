using FlexGuard.Core.Backup;
using FlexGuard.Core.Compression;
using FlexGuard.Core.Config;
using FlexGuard.Core.GroupCompression;
using FlexGuard.Core.Hashing;
using FlexGuard.Core.Manifest;
using FlexGuard.Core.Reporting;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FlexGuard.CLI.Library
{
    public static class OldCode
    {
        static void OldFunktionality()
        {
            const string configPath = "config.json";
            const bool diffMode = true;
            const string diffManifestFile = "Z:/FlexGuard/2025-07-20T1338_Full/manifest.json";
            //const string diffManifestFile = "Z:/FlexGuard/2025-07-20T1218_Full/manifest.json";

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

            // Determine backup type
            string backupType = diffMode ? "Diff" : "Full";
            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HHmm");
            string subfolderName = $"{timestamp}_{backupType}";
            string fullDestPath = Path.Combine(config.DestinationPath, subfolderName);
            Directory.CreateDirectory(fullDestPath);

            var compressor = new CompressorGZip();
            var hasher = new Sha256Hasher();
            var groupCompressor = new GroupCompressorZip(hasher, reporter);
            long maxBytesPerGroup = 1024 * 1024 * 1024;

            // Load previous manifest if diffMode
            BackupManifest? oldManifest = null;
            if (diffMode)
            {
                if (!File.Exists(diffManifestFile))
                {
                    reporter.Error($"Diff manifest file not found: {diffManifestFile}");
                    return;
                }

                var oldManifestJson = File.ReadAllText(diffManifestFile);
                oldManifest = JsonSerializer.Deserialize<BackupManifest>(oldManifestJson);

                if (oldManifest == null)
                {
                    reporter.Error("Failed to parse diff manifest file.");
                    return;
                }
            }

            AnsiConsole.Progress().Start(ctx =>
            {
                var task = ctx.AddTask($"[green]Backing up ({backupType})[/]");

                var progressReporter = new MessageReporterWithProgress(reporter, (current, total, file) =>
                {
                    if (task.MaxValue != total)
                        task.MaxValue = total;

                    task.Value = current;
                    task.Description = $"[green]Backing up: {Path.GetFileName(file)}[/]";
                });

                var backupProcessor = new BackupProcessorGroupFile(
                    groupCompressor,
                    maxFilesPerGroup: 1000,
                    maxBytesPerGroup: maxBytesPerGroup,
                    reporter: progressReporter);

                IBackupStrategy strategy = diffMode
                    ? new BackupStrategyDiff(backupProcessor, oldManifest!, progressReporter)
                    : new BackupStrategyFull(backupProcessor, progressReporter);

                strategy.RunBackup(config, fullDestPath, progressReporter);
            });

            stopwatch.Stop();
            reporter.Success($"{backupType} backup completed successfully!");
            reporter.Info($"Backup duration: {stopwatch.Elapsed:hh\\:mm\\:ss}");
        }
    }
}
