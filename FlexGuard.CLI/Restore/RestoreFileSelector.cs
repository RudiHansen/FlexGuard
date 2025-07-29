using FlexGuard.Core.Compression;
using FlexGuard.Core.Manifest;
using FlexGuard.Core.Registry;
using Spectre.Console;
using System.Text.Json;

namespace FlexGuard.CLI.Restore;

public class RestoreFileSelector
{
    private readonly BackupRegistry _registry;
    private readonly string _jobFolder;

    public RestoreFileSelector(BackupRegistry registry, string jobFolder)
    {
        _registry = registry;
        _jobFolder = jobFolder;
    }

    public record RestoreSelection(
        string RelativePath,
        string ChunkFile,
        long FileSize,
        string Hash,
        BackupRegistry.BackupEntry BackupEntry,
        CompressionMethod Compression);

    public List<RestoreSelection> SelectFiles()
    {
        AnsiConsole.Clear();

        // 1. Let user select which backup to restore from
        var manifestEntry = AnsiConsole.Prompt(
            new SelectionPrompt<BackupRegistry.BackupEntry>()
                .Title("Select a [green]backup version[/] to restore from")
                .UseConverter(entry => $"{entry.TimestampStart:yyyy-MM-dd HH:mm} - {entry.Type}")
                .AddChoices(_registry.Backups));

        var manifestPath = Path.Combine(_jobFolder, manifestEntry.ManifestFileName);

        if (!File.Exists(manifestPath))
        {
            AnsiConsole.MarkupLine($"[red]Manifest file not found: {manifestPath}[/]");
            return new();
        }

        // 2. Load manifest
        var manifestJson = File.ReadAllText(manifestPath);
        var manifest = JsonSerializer.Deserialize<FileManifest>(manifestJson);

        if (manifest == null)
        {
            AnsiConsole.MarkupLine("[red]Failed to deserialize manifest.[/]");
            return new();
        }

        // 3. Show file selector
        var allFiles = manifest.Files.Select(f => f.RelativePath).Distinct().OrderBy(p => p).ToList();
        var selectedPaths = DirectoryViewSelector.Show(allFiles);

        // 4. Match back to full manifest entries, including compression method
        var selections = manifest.Files
            .Where(f => selectedPaths.Contains(f.RelativePath))
            .Select(f => new RestoreSelection(
                f.RelativePath,
                f.ChunkFile,
                f.FileSize,
                f.Hash,
                manifestEntry,
                manifest.Compression))  // Include compression method
            .ToList();

        
        return selections;
    }
}