using Spectre.Console;
using System.IO;
using System.Linq;

namespace FlexGuard.CLI.Restore;

public static class DirectoryViewSelector
{
    private enum ViewMode
    {
        Directory,
        Tree
    }

    /// <summary>
    /// Displays a menu-based UI to switch between Directory View and Tree View.
    /// Returns the final list of selected files.
    /// </summary>
    public static List<string> Show(List<string> allFiles)
    {
        var root = BuildDirectoryTree(allFiles);
        var selectedItems = new HashSet<string>();
        var currentView = ViewMode.Directory;

        while (true)
        {
            // Show the main menu
            var menuChoice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Select view mode or finish[/]:")
                    .AddChoices(
                        currentView == ViewMode.Directory ? "▶ Directory View (current)" : "Directory View",
                        currentView == ViewMode.Tree ? "▶ Tree View (current)" : "Tree View",
                        "Finish and Restore"));

            if (menuChoice.StartsWith("Finish"))
                break; // Done, exit loop

            // Determine selected view
            currentView = menuChoice.StartsWith("Directory") ? ViewMode.Directory : ViewMode.Tree;

            // Build a list of choices for this view
            var items = currentView == ViewMode.Directory
                ? GetDirectoriesOnly(root)
                : GetDirectoriesAndFiles(root);

            // Show selection prompt
            var prompt = new MultiSelectionPrompt<string>()
                .Title($"Select [green]items[/] to restore - [yellow]{currentView} View[/]")
                .NotRequired()
                .PageSize(15)
                .MoreChoicesText("[grey](Move up/down to reveal more items)[/]")
                .InstructionsText("[grey](Press [blue]<space>[/] to toggle, [green]<enter>[/] to confirm)[/]")
                .AddChoices(items);

            // Preselect already chosen items
            foreach (var item in selectedItems.Intersect(items))
                prompt.Select(item);

            var choice = AnsiConsole.Prompt(prompt);

            // Update selected items
            selectedItems.RemoveWhere(x => items.Contains(x)); // Clear old selections from this view
            foreach (var c in choice)
                selectedItems.Add(c);
        }

        // Expand all selected directories to individual files
        var finalSelection = new HashSet<string>();
        foreach (var item in selectedItems)
        {
            if (item.EndsWith(Path.DirectorySeparatorChar))
            {
                AddAllFilesUnderFolder(root, item.TrimEnd(Path.DirectorySeparatorChar), finalSelection);
            }
            else
            {
                finalSelection.Add(item);
            }
        }

        return finalSelection.OrderBy(x => x).ToList();
    }

    // ----------------- Private Helper Methods -------------------

    private class DirNode
    {
        public string Name { get; set; } = "";
        public List<DirNode> Directories { get; } = new();
        public List<string> Files { get; } = new();
    }

    private static DirNode BuildDirectoryTree(List<string> allFiles)
    {
        var root = new DirNode { Name = "" };

        foreach (var filePath in allFiles)
        {
            var parts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var current = root;

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (i == parts.Length - 1)
                {
                    current.Files.Add(filePath);
                }
                else
                {
                    var dir = current.Directories.FirstOrDefault(d => d.Name == part);
                    if (dir == null)
                    {
                        dir = new DirNode { Name = part };
                        current.Directories.Add(dir);
                    }
                    current = dir;
                }
            }
        }
        return root;
    }

    private static List<string> GetDirectoriesOnly(DirNode root, string prefix = "")
    {
        var list = new List<string>();
        foreach (var dir in root.Directories.OrderBy(d => d.Name))
        {
            string fullPath = string.IsNullOrEmpty(prefix)
                ? dir.Name
                : Path.Combine(prefix, dir.Name);
            list.Add(fullPath + Path.DirectorySeparatorChar);
            list.AddRange(GetDirectoriesOnly(dir, fullPath));
        }
        return list;
    }

    private static List<string> GetDirectoriesAndFiles(DirNode root, string prefix = "")
    {
        var list = new List<string>();
        foreach (var dir in root.Directories.OrderBy(d => d.Name))
        {
            string fullPath = string.IsNullOrEmpty(prefix)
                ? dir.Name
                : Path.Combine(prefix, dir.Name);
            list.Add(fullPath + Path.DirectorySeparatorChar);
            list.AddRange(GetDirectoriesAndFiles(dir, fullPath));
        }
        list.AddRange(root.Files.OrderBy(f => f));
        return list;
    }

    private static void AddAllFilesUnderFolder(DirNode node, string folder, HashSet<string> result, string currentPath = "")
    {
        string currentFullPath = string.IsNullOrEmpty(currentPath)
            ? node.Name
            : Path.Combine(currentPath, node.Name);

        if (currentFullPath == folder)
        {
            AddAllFilesRecursive(node, result);
            return;
        }

        foreach (var subDir in node.Directories)
        {
            AddAllFilesUnderFolder(subDir, folder, result, currentFullPath);
        }
    }

    private static void AddAllFilesRecursive(DirNode node, HashSet<string> result)
    {
        foreach (var file in node.Files)
            result.Add(file);

        foreach (var subDir in node.Directories)
            AddAllFilesRecursive(subDir, result);
    }
}