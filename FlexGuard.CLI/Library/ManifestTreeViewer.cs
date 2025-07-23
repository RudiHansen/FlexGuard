using FlexGuard.Core.Manifest;
using Spectre.Console;
using System.Collections.Generic;
using System.Text.Json;

namespace FlexGuard.CLI.Library;

public static class ManifestTreeViewer
{
    public static string SelectFileFromManifest(string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            AnsiConsole.MarkupLine($"[red]Manifest not found: {manifestPath}[/]");
            return string.Empty;
        }

        var json = File.ReadAllText(manifestPath);
        var manifest = JsonSerializer.Deserialize<BackupManifest>(json);

        if (manifest == null || manifest.Files.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No files found in manifest.[/]");
            return string.Empty;
        }

        var tree = new Tree("[green]📁 Backup Root[/]");
        var rootNode = tree.AddNode("📁 /");
        var folderNodes = new Dictionary<string, TreeNode>
        {
            { "", rootNode }
        };

        foreach (var path in manifest.Files.Select(f => f.RelativePath))
        {
            var parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            var currentPath = "";
            var currentNode = rootNode;

            for (int i = 0; i < parts.Length - 1; i++)
            {
                currentPath = Path.Combine(currentPath, parts[i]);

                if (!folderNodes.TryGetValue(currentPath, out var nextNode))
                {
                    nextNode = currentNode.AddNode($"📁 {parts[i]}");
                    folderNodes[currentPath] = nextNode;
                }

                currentNode = nextNode;
            }

            var fileName = parts[^1];
            currentNode.AddNode($"📄 {fileName}");
        }

        AnsiConsole.Write(tree);

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select a file to restore:")
                .PageSize(15)
                .AddChoices(manifest.Files.Select(f => f.RelativePath)));

        return selected;
    }
}
