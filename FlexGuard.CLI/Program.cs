using FlexGuard.CLI.Entrypoint;
using FlexGuard.Core.Profiling;
using FlexGuard.Core.Util;
using FlexGuard.Core.Abstractions;                 // IFlexBackupEntryStore, ...
using FlexGuard.Core.Recording;                   // BackupRunRecorder  (din klasse)
using FlexGuard.Data.Repositories.Json;           // JsonFlexBackup* stores
using Microsoft.Extensions.DependencyInjection;
using FlexGuard.CLI.Infrastructure;

class Program
{
    static void Main(string[] args)
    {
        // ---- Minimal DI setup (ingen Host) ----
        var services = new ServiceCollection();

        // V�lg en base-mappe til JSON-filer (�ndr gerne til noget andet)
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var baseDir = Path.Combine(appData, "FlexGuard");

        // Registr�r JSON-stores med filstier
        services.AddSingleton<IFlexBackupEntryStore>(sp => new JsonFlexBackupEntryStore(Path.Combine(baseDir, "FlexBackupEntry.json")));
        services.AddSingleton<IFlexBackupChunkEntryStore>(sp => new JsonFlexBackupChunkEntryStore(Path.Combine(baseDir, "FlexBackupChunkEntry.json")));
        services.AddSingleton<IFlexBackupFileEntryStore>(sp => new JsonFlexBackupFileEntryStore(Path.Combine(baseDir, "FlexBackupFileEntry.json")));

        // Recorder �n pr. proces (du k�rer kun �t job ad gangen)
        services.AddSingleton<BackupRunRecorder>();

        // Byg provider og ekspon�r via en lille service-locator
        var provider = services.BuildServiceProvider();
        Services.Init(provider);


        PerformanceTracker.Instance.StartGlobal();
        CliEntrypoint.Run(args);
        PerformanceTracker.Instance.EndGlobal();
        var elapsed = PerformanceTracker.Instance.GetGlobalElapsed();
        if (elapsed.TotalMinutes >= 5)
        {
            NotificationHelper.PlayBackupCompleteSound();
        }
    }
}