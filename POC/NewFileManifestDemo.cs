using FlexGuard.Core.Abstractions;
using FlexGuard.Core.Compression;
using FlexGuard.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace POC
{
    internal static class NewFileManifestDemo
    {
        public static async Task RunAsync(ServiceProvider provider)
        {
            Console.WriteLine("=== NewFileManifest + Entries demo ===");

            var manifests = provider.GetRequiredService<INewFileManifestStore>();
            var entries = provider.GetRequiredService<INewFileManifestEntryStore>();

            // Opret et manifest
            var manifest = new NewFileManifest
            {
                // Id genereres automatisk i modellen, men du kan sætte selv hvis du vil:
                // Id = Ulid.NewUlid().ToString(),
                JobName = "Photos",
                Type = ManifestType.Full,
                TimestampUtc = DateTimeOffset.UtcNow,
                Compression = CompressionMethod.GZip,
                RunRefId = null, // hvis du ikke har et run endnu
                // CreatedUtc har default i modellen
            };
            await manifests.InsertAsync(manifest);

            // Hent alle og vis
            var allManifests = await manifests.GetAllAsync();
            Console.WriteLine($"Manifests: {allManifests.Count}");
            foreach (var m in allManifests) Console.WriteLine($"{m.Id} | {m.JobName} | {m.Type} | {m.TimestampUtc:u}");

            // Opdatér manifest (fx ændr Compression)
            var updated = new NewFileManifest
            {
                Id = manifest.Id,
                JobName = manifest.JobName,
                Type = ManifestType.Full,
                TimestampUtc = manifest.TimestampUtc,
                Compression = CompressionMethod.Brotli,
                RunRefId = manifest.RunRefId,
                CreatedUtc = manifest.CreatedUtc
            };
            await manifests.UpdateAsync(updated);

            // Opret en entry til manifestet
            var entry = new NewFileManifestEntry
            {
                // Id autogenereres, ellers: Id = Ulid.NewUlid().ToString(),
                ManifestId = updated.Id,
                RelativePath = "2025/10/report.pdf",
                ChunkFile = "chunks/aa/bb/aa-bb-cc.zst",
                FileSize = 1234567,
                LastWriteTimeUtc = DateTimeOffset.UtcNow.AddDays(-1),
                Hash = new string('a', 64), // placeholder 64-hex
                CompressionSkipped = false,
                CompressionRatio = 0.55m
            };
            await entries.InsertAsync(entry);

            // List entries for sanity (vi har ikke en ListByManifest, så vi viser bare GetAll og filtrerer)
            var allEntries = await entries.GetAllAsync();
            var forThis = allEntries.Where(e => e.ManifestId == updated.Id).ToList();
            Console.WriteLine($"Entries for manifest {updated.Id}: {forThis.Count}");
            foreach (var e in forThis) Console.WriteLine($" - {e.Id} | {e.RelativePath} | {e.FileSize} bytes");

            // Opdatér entry
            var entryUpdated = new NewFileManifestEntry
            {
                Id = entry.Id,
                ManifestId = entry.ManifestId,
                RelativePath = entry.RelativePath,
                ChunkFile = entry.ChunkFile,
                FileSize = entry.FileSize + 10,
                LastWriteTimeUtc = entry.LastWriteTimeUtc,
                Hash = entry.Hash,
                CompressionSkipped = entry.CompressionSkipped,
                CompressionRatio = entry.CompressionRatio
            };
            await entries.UpdateAsync(entryUpdated);

            // Slet entry igen (for at vise Delete)
            //await entries.DeleteAsync(entry.Id);

            // Til sidst: slet manifest (bemærk at entries ikke cascader i denne simple demo)
            //await manifests.DeleteAsync(updated.Id);

            Console.WriteLine();
        }
    }
}