namespace FlexGuard.Data.Configuration;

public sealed class FlexGuardDataOptions
{
    public Backend Backend { get; set; } = Backend.Json;
    // JSON-backend
    public string? JsonPath { get; set; } // fx %AppData%/FlexGuard/FlexTestTable.json

    // SQLite-backend
    public string? SqlitePath { get; set; } // fx %AppData%/FlexGuard/FlexTest.db

}