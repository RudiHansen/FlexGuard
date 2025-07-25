using Spectre.Console;

namespace FlexGuard.CLI.Restore
{
    public static class DirectoryViewSelector
    {
        /// <summary>
        /// Displays a directory tree view with selectable files and folders.
        /// Returns the selected file paths.
        /// </summary>
        public static List<string> Show(List<string> allFiles)
        {
            // Build a hierarchical structure
            var root = BuildDirectoryTree(allFiles);

            // Flat list of selectable items (files + folders)
            var selectableItems = new List<string>();
            CollectPaths(root, selectableItems, "");

            // Display multi-selection prompt
            var selected = AnsiConsole.Prompt(
                new MultiSelectionPrompt<string>()
                    .Title("Select [green]files or folders[/] to restore")
                    .NotRequired()
                    .PageSize(15)
                    .MoreChoicesText("[grey](Move up/down to reveal more items)[/]")
                    .InstructionsText("[grey](Press [blue]<space>[/] to toggle, [green]<enter>[/] to accept)[/]")
                    .AddChoices(selectableItems));

            // If user selects folders, expand them to include all files inside
            var finalSelection = new HashSet<string>();
            foreach (var item in selected)
            {
                if (DirectoryExistsInTree(item))
                {
                    // Add all files under this folder
                    AddAllFilesUnderFolder(root, item, finalSelection);
                }
                else
                {
                    finalSelection.Add(item);
                }
            }

            return finalSelection.OrderBy(x => x).ToList();
        }

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

        private static void CollectPaths(DirNode node, List<string> list, string prefix)
        {
            foreach (var dir in node.Directories.OrderBy(d => d.Name))
            {
                string fullPath = Path.Combine(prefix, dir.Name);
                list.Add(fullPath + Path.DirectorySeparatorChar); // Mark as folder
                CollectPaths(dir, list, fullPath);
            }
            foreach (var file in node.Files.OrderBy(f => f))
            {
                list.Add(file);
            }
        }

        private static bool DirectoryExistsInTree(string selectedPath)
        {
            return selectedPath.EndsWith(Path.DirectorySeparatorChar);
        }

        private static void AddAllFilesUnderFolder(DirNode node, string folder, HashSet<string> result, string currentPath = "")
        {
            string folderPath = folder.TrimEnd(Path.DirectorySeparatorChar);

            string currentFullPath = currentPath;
            if (!string.IsNullOrEmpty(node.Name))
                currentFullPath = Path.Combine(currentFullPath, node.Name);

            if (currentFullPath == folderPath)
            {
                // Add all files under this node
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
}