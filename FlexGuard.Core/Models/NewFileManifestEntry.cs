// FlexGuard.Core/Models/NewFileManifestEntry.cs
using NUlid;

namespace FlexGuard.Core.Models;

/// <summary>
/// Én fil i et manifest (metadata pr. fil for den kørsel).
/// </summary>
public sealed class NewFileManifestEntry
{
    /// <summary>System-id (ULID, 26 tegn).</summary>
    public string Id { get; init; } = Ulid.NewUlid().ToString();

    /// <summary>FK til NewFileManifest.Id (ULID, 26).</summary>
    public required string ManifestId { get; init; }

    /// <summary>Relativ sti fra jobroden (normaliseret). Max 1024.</summary>
    public required string RelativePath { get; init; }

    /// <summary>Sti/filnavn til chunk/arkiv med filens bytes for denne kørsel. Max 1024.</summary>
    public required string ChunkFile { get; init; }

    /// <summary>Logisk filstørrelse i bytes (ukomprimeret).</summary>
    public long FileSize { get; init; }

    /// <summary>Kilde-filens sidste skriv (UTC).</summary>
    public required DateTimeOffset LastWriteTimeUtc { get; init; }

    /// <summary>SHA-256 (hex, 64 tegn) af den logiske fil.</summary>
    public required string Hash { get; init; }

    /// <summary>Sandt hvis komprimering blev sprunget over.</summary>
    public bool CompressionSkipped { get; init; }

    /// <summary>Komprimeringsratio (compressed/original). Typisk 0–1. Null hvis ikke relevant.</summary>
    public decimal? CompressionRatio { get; init; }
}