using FlexGuard.Core.Config;
using FlexGuard.Core.Model;
using FlexGuard.Core.Reporting;

namespace FlexGuard.Core.Util;

public static class FileCollector
{
    public static List<PendingFileEntry> CollectFiles(BackupJobConfig config, IMessageReporter reporter)
    {
        var entries = new List<PendingFileEntry>();

        foreach (var source in config.Sources)
        {
            var files = FileEnumerator.GetFiles(source.Path, source.Exclude,reporter);
            foreach (var file in files)
            {
                try
                {
                    var info = new FileInfo(file);
                    entries.Add(new PendingFileEntry
                    {
                        SourcePath = file,
                        RelativePath = Path.GetRelativePath(source.Path, file),
                        FileSize = info.Length,
                        LastWriteTimeUtc = info.LastWriteTimeUtc
                    });
                }
                catch (Exception ex)
                {
                    // Log evt. fejl, f.eks. adgang nægtet
                    // (kan forbedres med IMessageReporter senere)
                    reporter.Warning($"Could not access file: {file} - {ex.Message}");
                }
            }
        }

        return entries;
    }
}