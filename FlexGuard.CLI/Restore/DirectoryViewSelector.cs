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
    /// Displays a menu-based UI to switch between Directory View and Tree View.
    /// Supports filtering, select-all, and clear-all operations.
    /// Returns the final list of selected files.
    /// </summary>
    public static List<string> Show(List<string> allFiles)
    {
        var root = BuildDirectoryTree(allFiles);
        var selectedItems = new HashSet<string>();
        var currentView = ViewMode.Directory;
        string currentFilter = "";

        while (true)
        {
            // Main menu
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

                // Apply current filter
                allItems = allItems
                    .Where(i => string.IsNullOrEmpty(currentFilter) || i.Contains(currentFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var item in allItems)
                {
                    if (item.EndsWith(Path.DirectorySeparatorChar))
                        AddAllUnder(root, item.TrimEnd(Path.DirectorySeparatorChar), selectedItems);
                    else
                        selectedItems.Add(item);
                }

                AnsiConsole.MarkupLine("[grey]All items selected.[/]");
                continue;
            }

            if (menuChoice == "Filter list")
            {
                var input = AnsiConsole.Prompt(
                    new TextPrompt<string>("Enter filter (leave empty to reset):")
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

                // Test if filter matches any items
                var testItems = (currentView == ViewMode.Directory
                    ? GetDirectoriesOnly(root)
                    : GetDirectoriesAndFiles(root))
                    .Where(i => string.IsNullOrEmpty(currentFilter) || i.Contains(currentFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (testItems.Count == 0)
                {
                    AnsiConsole.MarkupLine("[red]No items match the current filter.[/]");
                    currentFilter = ""; // Reset automatically
                }
                continue;
            }

            // Switch view mode
            currentView = menuChoice.Contains("Directory") ? ViewMode.Directory : ViewMode.Tree;

            if (currentView == ViewMode.Tree)
                ExpandSelectedDirectories(selectedItems, root);

            // Build the item list for the current view
            var items = (currentView == ViewMode.Directory
                ? GetDirectoriesOnly(root)
                : GetDirectoriesAndFiles(root))
                .Where(i => string.IsNullOrEmpty(currentFilter) || i.Contains(currentFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Display multi-selection prompt
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

            // Synchronize selections
            SyncSelections(selectedItems, choice, items, root);
        }

        // Expand final selection of directories into files
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

    // ----------------- Helper Methods -------------------

    /// <summary>
    /// Synchronizes selection changes from the prompt with the global selection set.
    /// </summary>
    private static void SyncSelections(HashSet<string> selectedItems, List<string> choice, List<string> items, DirNode root)
    {
        // Remove items that were deselected
        foreach (var item in items)
        {
            if (!choice.Contains(item) && selectedItems.Contains(item))
            {
                if (item.EndsWith(Path.DirectorySeparatorChar))
                    RemoveAllUnder(item, selectedItems);
                selectedItems.Remove(item);
            }
        }

        // Add items that were newly selected
        foreach (var item in choice)
        {
            if (!selectedItems.Contains(item))
            {
                if (item.EndsWith(Path.DirectorySeparatorChar))
                    AddAllUnder(root, item.TrimEnd(Path.DirectorySeparatorChar), selectedItems);
                else
                    selectedItems.Add(item);
            }
        }
    }

    /// <summary>
    /// Normalizes directory paths to always end with a directory separator.
    /// </summary>
    private static string NormalizeDir(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    /// <summary>
    /// Removes all files and sub-directories under a given directory from the selection.
    /// </summary>
    private static void RemoveAllUnder(string folderPath, HashSet<string> selected)
    {
        var prefix = NormalizeDir(folderPath);
        selected.RemoveWhere(p =>
            p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Adds all directories and files under a folder to the selection.
    /// </summary>
    private static void AddAllUnder(DirNode root, string folderPath, HashSet<string> selected)
    {
        selected.Add(NormalizeDir(folderPath));
        AddAllDirectoriesAndFilesUnderFolder(root, folderPath, selected);
    }

    // ----------------- Tree Builders -------------------

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

    /// <summary>
    /// Ensures that all files under selected directories are included when switching to Tree View.
    /// </summary>
    private static void ExpandSelectedDirectories(HashSet<string> selectedItems, DirNode root)
    {
        foreach (var dir in selectedItems.Where(i => i.EndsWith(Path.DirectorySeparatorChar)).ToList())
        {
            AddAllFilesUnderFolder(root, dir.TrimEnd(Path.DirectorySeparatorChar), selectedItems);
        }
    }

    /// <summary>
    /// Adds all files under a folder to the selection (but not sub-folders).
    /// </summary>
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

    /// <summary>
    /// Adds all directories and files under a folder to the selection.
    /// </summary>
    private static void AddAllDirectoriesAndFilesUnderFolder(DirNode node, string folder, HashSet<string> result, string currentPath = "")
    {
        string currentFullPath = string.IsNullOrEmpty(currentPath)
            ? node.Name
            : Path.Combine(currentPath, node.Name);

        if (currentFullPath == folder)
        {
            AddAllDirsAndFilesRecursive(node, currentFullPath, result);
            return;
        }

        foreach (var subDir in node.Directories)
        {
            AddAllDirectoriesAndFilesUnderFolder(subDir, folder, result, currentFullPath);
        }
    }

    private static void AddAllDirsAndFilesRecursive(DirNode node, string basePath, HashSet<string> result)
    {
        result.Add(basePath + Path.DirectorySeparatorChar);

        foreach (var file in node.Files)
            result.Add(file);

        foreach (var subDir in node.Directories)
        {
            string subPath = Path.Combine(basePath, subDir.Name);
            AddAllDirsAndFilesRecursive(subDir, subPath, result);
        }
    }
}