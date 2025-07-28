using FlexGuard.CLI.Entrypoint;
using FlexGuard.Core.Profiling;
using FlexGuard.Core.Util;

class Program
{
    static void Main(string[] args)
    {
        PerformanceTracker.Instance.StartGlobal();
        CliEntrypoint.Run(args);
        PerformanceTracker.Instance.EndGlobal();
        NotificationHelper.PlayBackupCompleteSound();
    }
}