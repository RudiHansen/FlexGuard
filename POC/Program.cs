using FlexGuard.Core.Abstractions;
using FlexGuard.Data.Repositories.Json;
using FlexGuard.Data.Repositories.Sqlite;
using FlexGuard.Data.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace POC
{
    internal class Program
    {
        public static async Task Main()
        {
            await RunPerformanceMonitorTest.RunDemoAsync();
            //TestDataFunctionality();
        }
        private static async void TestDataFunctionality()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var baseDir = Path.Combine(appData, "FlexGuard");
            var jsonTestPath = Path.Combine(baseDir, "FlexTestTable.json");
            var sqliteDbPath = Path.Combine(baseDir, "FlexGuard.db");

            Directory.CreateDirectory(baseDir);

            DapperTypeHandlers.EnsureRegistered();
            var services = new ServiceCollection();

            // FlexTestTable -> JSON
            services.AddSingleton<IFlexTestTableStore>(_ => new JsonFlexTestTableStore(jsonTestPath));

            // NewFileManifest -> SQLite
            services.AddSingleton<INewFileManifestStore>(_ => new SqliteNewFileManifestStore(sqliteDbPath));

            // NewFileManifestEntry -> SQLite
            services.AddSingleton<INewFileManifestEntryStore>(_ => new SqliteNewFileManifestEntryStore(sqliteDbPath));

            var provider = services.BuildServiceProvider();

            // Kør de to små “tests” separat
            await NewFileManifestDemo.RunAsync(provider);
            await FlexTestTableDemo.RunAsync(provider);

            Console.WriteLine("\nDone.");

        }
    }
}