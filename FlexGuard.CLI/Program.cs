using FlexGuard.CLI.Entrypoint;
using FlexGuard.CLI.Infrastructure;
using FlexGuard.Core.Abstractions;                 // IFlexBackupEntryStore, ...
using FlexGuard.Core.Recording;                   // BackupRunRecorder  (din klasse)
using FlexGuard.Core.Util;
using FlexGuard.Data.Repositories.Json;           // JsonFlexBackup* stores
using FlexGuard.Data.Repositories.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

class Program
{
    static async Task Main(string[] args)
    {
        // ---- Minimal DI setup (ingen Host) ----
        var services = new ServiceCollection();

        // Vælg en base-mappe til JSON-filer (ændr gerne til noget andet)
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var baseDir = Path.Combine(appData, "FlexGuard");
        var sqliteDbPath = Path.Combine(baseDir, "FlexGuard.db");

        // Registrér JSON-stores med filstier
        services.AddSingleton<IFlexBackupEntryStore>(sp => new SqliteFlexBackupEntryStore(sqliteDbPath));
        services.AddSingleton<IFlexBackupChunkEntryStore>(sp => new SqliteFlexBackupChunkEntryStore(sqliteDbPath));
        services.AddSingleton<IFlexBackupFileEntryStore>(sp => new SqliteFlexBackupFileEntryStore(sqliteDbPath));

        // Recorder én pr. proces (du kører kun ét job ad gangen)
        services.AddSingleton<BackupRunRecorder>();

        // Byg provider og eksponér via en lille service-locator
        var provider = services.BuildServiceProvider();
        Services.Init(provider);


        var sw = Stopwatch.StartNew();
        await CliEntrypoint.RunAsync(args);
        sw.Stop();
        if (sw.Elapsed.TotalMinutes >= 5)
        {
            NotificationHelper.PlayBackupCompleteSound();
        }
    }
}