using Spectre.Console;

namespace FlexGuard.CLI.Restore;

public static class DirectoryViewSelector
{
    private enum ViewMode
    {
        Directory,
        Tree
    }

    /// <summary>
    /// Displays a menu-based UI to switch between Directory View and Tree View, with filtering and selection tools.
    /// </summary>
    public static List<string> Show(List<string> allFiles)
    {
        var root = BuildDirectoryTree(allFiles);
        var selectedItems = new HashSet<string>();
        var currentView = ViewMode.Directory;
        string currentFilter = "";

        while (true)
        {
            // Build main menu
            var menuChoice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Select view mode, manage selections, or finish[/]:")
                    .AddChoices(
                        currentView == ViewMode.Directory ? "▶ Directory View (current)" : "Directory View",
                        currentView == ViewMode.Tree ? "▶ Tree View (current)" : "Tree View",
                        "Select all items",
                        "Clear all selections",
                        "Filter list",
                        "Finish and Restore"));

            if (menuChoice == "Finish and Restore")
                break;

            if (menuChoice == "Clear all selections")
            {
                selectedItems.Clear();
                AnsiConsole.MarkupLine("[grey]All selections cleared.[/]");
                continue;
            }

            if (menuChoice == "Select all items")
            {
                var allItems = currentView == ViewMode.Directory
                    ? GetDirectoriesOnly(root)
                    : GetDirectoriesAndFiles(root);

                // Apply filter
                allItems = allItems
                    .Where(i => string.IsNullOrEmpty(currentFilter) || i.Contains(currentFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var item in allItems)
                {
                    selectedItems.Add(item);
                    if (item.EndsWith(Path.DirectorySeparatorChar))
                        AddAllFilesUnderFolder(root, item.TrimEnd(Path.DirectorySeparatorChar), selectedItems);
                }

                AnsiConsole.MarkupLine("[grey]All items selected.[/]");
                continue;
            }

            if (menuChoice == "Filter list")
            {
                var input = AnsiConsole.Prompt(new TextPrompt<string>("Enter filter (leave empty to reset):")
                    .AllowEmpty()
                    .PromptStyle("yellow")
                ).Trim();
                if (string.IsNullOrEmpty(input))
                {
                    currentFilter = "";
                    AnsiConsole.MarkupLine("[grey]Filter cleared, showing all items.[/]");
                }
                else
                {
                    currentFilter = input;
                    AnsiConsole.MarkupLine($"[grey]Filter set to:[/] [yellow]{currentFilter}[/]");
                }

                // Check if filter matches any items
                var testItems = (currentView == ViewMode.Directory
                    ? GetDirectoriesOnly(root)
                    : GetDirectoriesAndFiles(root))
                    .Where(i => string.IsNullOrEmpty(currentFilter) || i.Contains(currentFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (testItems.Count == 0)
                {
                    AnsiConsole.MarkupLine("[red]No items match the current filter.[/]");
                    currentFilter = ""; // Reset filter automatically
                }
                continue;
            }

            // Change view mode
            currentView = menuChoice.Contains("Directory") ? ViewMode.Directory : ViewMode.Tree;

            // Expand selected directories when switching to Tree View
            if (currentView == ViewMode.Tree)
                ExpandSelectedDirectories(selectedItems, root);

            // Build items list
            var items = (currentView == ViewMode.Directory
                ? GetDirectoriesOnly(root)
                : GetDirectoriesAndFiles(root))
                .Where(i => string.IsNullOrEmpty(currentFilter) || i.Contains(currentFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Show multi-selection
            var prompt = new MultiSelectionPrompt<string>()
                .Title($"Select [green]items[/] to restore - [yellow]{currentView} View[/]")
                .NotRequired()
                .PageSize(15)
                .MoreChoicesText("[grey](Move up/down to reveal more items)[/]")
                .InstructionsText("[grey](Press [blue]<space>[/] to toggle, [green]<enter>[/] to confirm)[/]")
                .AddChoices(items);

            foreach (var item in selectedItems.Intersect(items))
                prompt.Select(item);

            var choice = AnsiConsole.Prompt(prompt);

            // Sync current selections
            SyncSelections(selectedItems, choice, items, root);
        }

        // Final expansion of directories into files
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

    // ----------------- Helper methods -------------------

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

    private static void SyncSelections(HashSet<string> selectedItems, List<string> choice, List<string> items, DirNode root)
    {
        // Remove deselected
        foreach (var item in items)
        {
            if (!choice.Contains(item) && selectedItems.Contains(item))
            {
                if (item.EndsWith(Path.DirectorySeparatorChar))
                {
                    RemoveAllDirectoriesAndFilesUnderFolder(root, item.TrimEnd(Path.DirectorySeparatorChar), selectedItems);
                }
                else
                {
                    selectedItems.Remove(item);
                }
            }
        }

        // Add newly selected
        foreach (var item in choice)
        {
            if (!selectedItems.Contains(item))
            {
                selectedItems.Add(item);
                if (item.EndsWith(Path.DirectorySeparatorChar))
                {
                    AddAllDirectoriesAndFilesUnderFolder(root, item.TrimEnd(Path.DirectorySeparatorChar), selectedItems);
                }
            }
        }
    }
    private static void ExpandSelectedDirectories(HashSet<string> selectedItems, DirNode root)
    {
        foreach (var dir in selectedItems.Where(i => i.EndsWith(Path.DirectorySeparatorChar)).ToList())
        {
            AddAllFilesUnderFolder(root, dir.TrimEnd(Path.DirectorySeparatorChar), selectedItems);
        }
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

    private static void RemoveAllFilesUnderFolder(DirNode node, string folder, HashSet<string> selected, string currentPath = "")
    {
        string currentFullPath = string.IsNullOrEmpty(currentPath)
            ? node.Name
            : Path.Combine(currentPath, node.Name);

        if (currentFullPath == folder)
        {
            foreach (var file in node.Files)
                selected.Remove(file);
            foreach (var subDir in node.Directories)
                RemoveAllFilesUnderFolder(subDir, folder, selected, currentFullPath);
            return;
        }

        foreach (var subDir in node.Directories)
        {
            RemoveAllFilesUnderFolder(subDir, folder, selected, currentFullPath);
        }
    }
    private static void AddAllDirectoriesAndFilesUnderFolder(DirNode node, string folder, HashSet<string> result, string currentPath = "")
    {
        string currentFullPath = string.IsNullOrEmpty(currentPath)
            ? node.Name
            : Path.Combine(currentPath, node.Name);

        if (currentFullPath == folder)
        {
            // Tilføj mappen selv
            result.Add(currentFullPath + Path.DirectorySeparatorChar);

            // Tilføj alle under-mapper og filer
            AddAllDirectoriesRecursive(node, result, currentFullPath);
            return;
        }

        foreach (var subDir in node.Directories)
        {
            AddAllDirectoriesAndFilesUnderFolder(subDir, folder, result, currentFullPath);
        }
    }

    private static void AddAllDirectoriesRecursive(DirNode node, HashSet<string> result, string currentPath)
    {
        foreach (var subDir in node.Directories)
        {
            string dirPath = Path.Combine(currentPath, subDir.Name);
            result.Add(dirPath + Path.DirectorySeparatorChar);
            AddAllDirectoriesRecursive(subDir, result, dirPath);
        }

        foreach (var file in node.Files)
            result.Add(file);
    }
    private static void RemoveAllDirectoriesAndFilesUnderFolder(DirNode node, string folder, HashSet<string> selected, string currentPath = "")
    {
        string currentFullPath = string.IsNullOrEmpty(currentPath)
            ? node.Name
            : Path.Combine(currentPath, node.Name);

        if (currentFullPath == folder)
        {
            selected.Remove(currentFullPath + Path.DirectorySeparatorChar);

            foreach (var file in node.Files)
                selected.Remove(file);

            foreach (var subDir in node.Directories)
                RemoveAllDirectoriesAndFilesUnderFolder(subDir, folder, selected, currentFullPath);
            return;
        }

        foreach (var subDir in node.Directories)
        {
            RemoveAllDirectoriesAndFilesUnderFolder(subDir, folder, selected, currentFullPath);
        }
    }
}
