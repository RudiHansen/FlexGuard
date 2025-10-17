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
            services.AddFlexGuardData(o =>
            {
                //o.Backend = Backend.Json;
                //o.JsonPath = Path.Combine(appData, "FlexGuard", "FlexTestTable.json");
                o.Backend = Backend.Sqlite;
                o.SqlitePath = Path.Combine(appData, "FlexGuard", "FlexTest.db");
            });

            var provider = services.BuildServiceProvider();

            // Hent store
            var store = provider.GetRequiredService<IFlexTestTableStore>();

            // Insert new records
            await store.InsertAsync(new FlexTestRow { TestNavn = "Hej verden", Pris = 5.5m, Type = TestType.Medium });
            await store.InsertAsync(new FlexTestRow { TestNavn = "Demo", Pris = 7.5m, Type = TestType.Normal });

            // GetAll
            string savedId = "";
            var all = await store.GetAllAsync();
            Console.WriteLine($"Rows after insert: {all.Count}");
            foreach (var r in all)
            {
                Console.WriteLine($"{r.Id}: {r.TestNavn}");
                savedId = r.Id;
            }

            // GetById
            var one = await store.GetByIdAsync(savedId);
            Console.WriteLine($"GetById({savedId}) -> {(one is null ? "null" : one.TestNavn)}");

            //Update One record and Insert another
            await store.UpdateAsync(new FlexTestRow { Id = savedId, TestNavn = "Opdateret tekst", Pris = 1.5m, Type = TestType.Medium });
            await store.InsertAsync(new FlexTestRow { TestNavn = "Task4", Pris = 2.5m, Type = TestType.Medium });

            // Show updated list
            var finalRows = await store.GetAllAsync();
            Console.WriteLine($"Rows after update/insert: {finalRows.Count}");
            foreach (var r in finalRows) Console.WriteLine($"{r.Id}: {r.TestNavn}");

            // Delete one record
            await store.DeleteAsync(savedId);

            // Vis slutresultat
            finalRows = await store.GetAllAsync();
            Console.WriteLine($"Rows after delete: {finalRows.Count}");
            foreach (var r in finalRows)
            {
                Console.WriteLine($"{r.Id}: {r.TestNavn}");
            }

            Console.WriteLine("Done. JSON path:");
            Console.WriteLine(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FlexGuard", "FlexTestTable.json"));
            Console.WriteLine(dbPath);
        }
    }
}