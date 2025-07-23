using FlexGuard.Core.Manifest;
using FlexGuard.Core.Restore;
using Spectre.Console;

namespace FlexGuard.CLI.Library;

public static class RestoreSelector
{
    public static List<string> PromptRestoreFilesFromManifest(string manifestPath)
    {
        var manifest = RestoreHelper.Load(manifestPath);

        // Trin 1: Hent alle relative paths
        var allFiles = manifest.Files.Select(f => f.RelativePath).Distinct().ToList();

        // Trin 2: Find top-level directories
        var topDirs = allFiles
            .Select(p => p.Split('/')[0]) // "Billeder/foto.jpg" → "Billeder"
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        // Trin 3: Valg: mapper eller enkelte filer
        var promptChoices = new List<string>();
        promptChoices.AddRange(topDirs.Select(d => $"[[DIR]] {d}"));
        //promptChoices.AddRange(allFiles.OrderBy(p => p).Take(20)); 
        promptChoices.AddRange(allFiles.OrderBy(p => p)); 

        var selected = AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>()
                .Title("Vælg mapper eller filer til gendannelse:")
                .NotRequired()
                .PageSize(20)
                .InstructionsText("(Brug [blue]<space>[/] til at vælge, [green]<enter>[/] for at bekræfte)")
                .AddChoices(promptChoices));

        // Trin 4: Find filer under valgte mapper + direkte valgte filer
        var selectedFiles = new HashSet<string>();

        foreach (var item in selected)
        {
            if (item.StartsWith("[[DIR]] "))
            {
                var dir = item.Substring(6);
                foreach (var f in allFiles.Where(f => f.StartsWith($"{dir}/")))
                    selectedFiles.Add(f);
            }
            else
            {
                selectedFiles.Add(item);
            }
        }

        // Trin 5: Bekræft
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Følgende filer gendannes:[/]");
        foreach (var f in selectedFiles.OrderBy(f => f))
            AnsiConsole.MarkupLine($"[green]✓[/] {f}");

        var confirm = AnsiConsole.Confirm("Vil du fortsætte med at gendanne disse filer?");
        return confirm ? selectedFiles.ToList() : new List<string>();
    }
}