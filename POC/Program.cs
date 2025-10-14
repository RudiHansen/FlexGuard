using FlexGuard.Core.Abstractions;
using FlexGuard.Core.Models;
using FlexGuard.Data.Configuration;
using FlexGuard.Data.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace POC
{
    internal class Program
    {
        public static async Task Main()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dbPath = Path.Combine(appData, "FlexGuard", "FlexTest.db");

            var services = new ServiceCollection();
            //services.AddFlexGuardData(o => {
            //    o.Backend = Backend.Sqlite;
            //    o.SqlitePath = Path.Combine(appData, "FlexGuard", "FlexTest.db");
            //});
            services.AddFlexGuardData(o =>
            {
                o.Backend = Backend.Json;
                o.JsonPath = Path.Combine(appData, "FlexGuard", "FlexTestTable.json");
            });
            var provider = services.BuildServiceProvider();

            // Hent store
            var store = provider.GetRequiredService<IFlexTestTableStore>();

            // 1) Upsert
            await store.UpsertAsync(new FlexTestRow { Id = 1, TestNavn = "Hej verden" });
            await store.UpsertAsync(new FlexTestRow { Id = 2, TestNavn = "Demo" });

            // 2) GetAll
            var all = await store.GetAllAsync();
            Console.WriteLine($"Rows after insert: {all.Count}");
            foreach (var r in all) Console.WriteLine($"{r.Id}: {r.TestNavn}");

            // 3) GetById
            var one = await store.GetByIdAsync(1);
            Console.WriteLine($"GetById(1) -> {(one is null ? "null" : one.TestNavn)}");

            // 4) Update (Upsert igen)
            await store.UpsertAsync(new FlexTestRow { Id = 1, TestNavn = "Opdateret tekst" });
            await store.UpsertAsync(new FlexTestRow { Id = 4, TestNavn = "Task4" });

            // 5) Delete
            await store.DeleteAsync(2);

            // 6) Vis slutresultat
            var finalRows = await store.GetAllAsync();
            Console.WriteLine($"Rows after update/delete: {finalRows.Count}");
            foreach (var r in finalRows) Console.WriteLine($"{r.Id}: {r.TestNavn}");

            Console.WriteLine("Done. JSON path:");
            Console.WriteLine(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FlexGuard", "FlexTestTable.json"));
            Console.WriteLine(dbPath);
        }
    }
}
