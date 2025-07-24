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

    public List<string> SelectFiles()
    {
        // 1. Brugeren vælger en manifest via registry
        var manifestEntry = AnsiConsole.Prompt(
            new SelectionPrompt<BackupRegistry.BackupEntry>()
                .Title("Select a [green]backup version[/] to restore from")
                .UseConverter(entry => $"{entry.Timestamp:yyyy-MM-dd HH:mm} - {entry.Type}")
                .AddChoices(_registry.Backups));

        var manifestPath = Path.Combine(_jobFolder, manifestEntry.ManifestFileName);

        if (!File.Exists(manifestPath))
        {
            AnsiConsole.MarkupLine($"[red]Manifest file not found: {manifestPath}[/]");
            return new();
        }

        // 2. Load manifest
        var manifestJson = File.ReadAllText(manifestPath);
        var manifest = JsonSerializer.Deserialize<BackupManifest>(manifestJson);

        if (manifest == null)
        {
            AnsiConsole.MarkupLine("[red]Failed to deserialize manifest.[/]");
            return new();
        }

        // 3. Vis valg af filer
        var allFiles = manifest.Files.Select(f => f.RelativePath).Distinct().OrderBy(p => p).ToList();
        var selected = AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>()
                .Title("Select [green]files to restore[/]")
                .NotRequired()
                .PageSize(15)
                .MoreChoicesText("[grey](Move up/down to reveal more files)[/]")
                .InstructionsText("[grey](Press [blue]<space>[/] to toggle a file, [green]<enter>[/] to accept)[/]")
                .AddChoices(allFiles));

        return selected;
    }
}
