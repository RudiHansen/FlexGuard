using FlexGuard.Core.Reporting;
using System.IO;

namespace FlexGuard.Core.Backup;

public static class FileEnumerator
{
    public static IEnumerable<string> GetFiles(string rootPath, List<string> excludePatterns, IMessageReporter reporter)
    {
        var stack = new Stack<string>();
        stack.Push(rootPath);

        while (stack.Count > 0)
        {
            var currentDir = stack.Pop();

            if (ShouldExclude(currentDir, rootPath, excludePatterns))
                continue;

            string[]? files = null;
            try
            {
                files = Directory.GetFiles(currentDir);
            }
            catch (Exception ex)
            {
                reporter.Warning($"Could not access files in '{currentDir}': {ex.Message}");
            }

            if (files != null)
            {
                foreach (var file in files)
                {
                    if (!ShouldExclude(file, rootPath, excludePatterns))
                        yield return file;
                }
            }

            string[]? subdirs = null;
            try
            {
                subdirs = Directory.GetDirectories(currentDir);
            }
            catch (Exception ex)
            {
                reporter.Warning($"Could not access subdirectories in '{currentDir}': {ex.Message}");
            }

            if (subdirs != null)
            {
                foreach (var subdir in subdirs)
                {
                    stack.Push(subdir);
                }
            }
        }
    }

    private static bool ShouldExclude(string fullPath, string rootPath, List<string> patterns)
    {
        string relativePath;
        try
        {
            relativePath = Path.GetRelativePath(rootPath, fullPath)
                                .Replace('\\', '/')
                                .TrimEnd('/');
        }
        catch
        {
            return false; // fallback to including the file
        }

        foreach (var pattern in patterns)
        {
            var normalizedPattern = pattern.Replace('\\', '/').TrimEnd('/');

            if (relativePath.Equals(normalizedPattern, StringComparison.OrdinalIgnoreCase) ||
                relativePath.StartsWith(normalizedPattern + "/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
