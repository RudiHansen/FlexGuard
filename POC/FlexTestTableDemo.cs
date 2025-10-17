using FlexGuard.Core.Abstractions;
using FlexGuard.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace POC
{
    internal static class FlexTestTableDemo
    {
        public static async Task RunAsync(ServiceProvider provider)
        {
            Console.WriteLine("=== FlexTestTable demo ===");
            var store = provider.GetRequiredService<IFlexTestTableStore>();

            // Insert
            await store.InsertAsync(new FlexTestRow { TestNavn = "Hej verden", Pris = 5.5m, Type = TestType.Medium });
            await store.InsertAsync(new FlexTestRow { TestNavn = "Demo", Pris = 7.5m, Type = TestType.Normal });

            // GetAll
            var all = await store.GetAllAsync();
            Console.WriteLine($"Rows after insert: {all.Count}");
            foreach (var r in all) Console.WriteLine($"{r.Id}: {r.TestNavn} ({r.Type}, {r.Pris})");

            // Vælg én og opdatér
            var first = all[0];
            await store.UpdateAsync(new FlexTestRow { Id = first.Id, TestNavn = "Opdateret tekst", Pris = 1.5m, Type = TestType.Medium });

            // Insert endnu én
            await store.InsertAsync(new FlexTestRow { TestNavn = "Task4", Pris = 2.5m, Type = TestType.Medium });

            // Vis liste igen
            var finalRows = await store.GetAllAsync();
            Console.WriteLine($"Rows after update/insert: {finalRows.Count}");
            foreach (var r in finalRows) Console.WriteLine($"{r.Id}: {r.TestNavn}");

            // Delete den første
            await store.DeleteAsync(first.Id);

            // Slut
            finalRows = await store.GetAllAsync();
            Console.WriteLine($"Rows after delete: {finalRows.Count}");
            foreach (var r in finalRows) Console.WriteLine($"{r.Id}: {r.TestNavn}");
            Console.WriteLine();
        }
    }
}