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
    /// Displays a directory selection UI with [Tab] to switch between Directory View (folders only)
    /// and Tree View (folders + files). Returns the final list of selected files.
    /// </summary>
    public static List<string> Show(List<string> allFiles)
    {
        var root = BuildDirectoryTree(allFiles);
        var selectedItems = new HashSet<string>();
        var currentView = ViewMode.Directory;

        while (true)
        {
            // Build a list of choices depending on the current view mode
            var items = currentView == ViewMode.Directory
                ? GetDirectoriesOnly(root)
                : GetDirectoriesAndFiles(root);

            // Display current view mode
            AnsiConsole.MarkupLine($"[grey]View mode:[/] [yellow]{currentView} View[/] (Press [blue]<Tab>[/] to switch)");

            // Display selection prompt
            var prompt = new MultiSelectionPrompt<string>()
                .Title("Select [green]files or folders[/] to restore")
                .NotRequired()
                .PageSize(15)
                .MoreChoicesText("[grey](Move up/down to reveal more items)[/]")
                .InstructionsText("[grey](Press [blue]<space>[/] to toggle, [blue]<Tab>[/] to switch view, [green]<enter>[/] to accept)[/]")
                .AddChoices(items);

            // Preselect previously chosen items (intersection)
            prompt.AddChoices(items);

            // Preselect previously chosen items
            foreach (var item in selectedItems.Intersect(items))
            {
                prompt.Select(item);
            }

            var choice = AnsiConsole.Prompt(prompt);

            // Detect if the user pressed [Tab] to switch view
            var lastKey = AnsiConsole.Console.Input.ReadKey(true);
            if (lastKey.HasValue && lastKey.Value.Key == ConsoleKey.Tab)
            {
                // Update selectedItems
                foreach (var c in choice) selectedItems.Add(c);
                currentView = currentView == ViewMode.Directory ? ViewMode.Tree : ViewMode.Directory;
                AnsiConsole.Clear();
                continue;
            }

            // Final selection when user presses [Enter]
            foreach (var c in choice) selectedItems.Add(c);
            break;
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