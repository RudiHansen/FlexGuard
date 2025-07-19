namespace FlexGuard.Core.Util;

public static class FileEnumerator
{
    public static IEnumerable<string> GetFiles(string rootPath, List<string> excludePatterns)
    {
        return Directory.GetFiles(rootPath, "*.*", SearchOption.AllDirectories)
            .Where(file => !excludePatterns.Any(p => file.Contains(p)));
    }
}