namespace FlexGuard.Data.Configuration;

public sealed class FlexGuardDataOptions
{
    public Backend Backend { get; set; } = Backend.Json;
    // JSON-backend
    public string? JsonPath { get; set; } // fx %AppData%/FlexGuard/FlexTestTable.json
    public string? JsonManifestPath { get; set; }        // NewFileManifest.json
    public string? JsonManifestEntryPath { get; set; }   // NewFileManifestEntry.json

    // NEW: backup-related JSON paths
    public string? JsonBackupEntryPath { get; set; }
    public string? JsonBackupChunkEntryPath { get; set; }
    public string? JsonBackupFileEntryPath { get; set; }

    // SQLite-backend
    public string? SqlitePath { get; set; } // fx %AppData%/FlexGuard/FlexTest.db

}