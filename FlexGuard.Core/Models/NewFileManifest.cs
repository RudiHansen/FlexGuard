using FlexGuard.Core.Compression;
using NUlid;

namespace FlexGuard.Core.Models;

/// <summary>
/// Manifest-header for én backup-kørsel.
/// </summary>
public sealed class NewFileManifest
{
    /// <summary>System-id (ULID, 26 tegn).</summary>
    public string Id { get; init; } = Ulid.NewUlid().ToString();

    /// <summary>Logisk jobnavn (≤ 50).</summary>
    public required string JobName { get; init; }

    /// <summary>Kørselstype (fx Full, Diff).</summary>
    public ManifestType Type { get; init; } = ManifestType.None;

    /// <summary>Tidspunkt for manifestet (UTC).</summary>
    public required DateTimeOffset TimestampUtc { get; init; }

    /// <summary>Komprimeringsmetode brugt for kørslen.</summary>
    public CompressionMethod Compression { get; init; } = CompressionMethod.Zstd;

    /// <summary>Valgfri reference til tilhørende run (kan erstattes af ULID senere).</summary>
    public long? RunRefId { get; init; }

    /// <summary>Oprettelsestidspunkt (UTC) for rækken.</summary>
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Manifestets kørselstype.</summary>
public enum ManifestType
{
    None = 0,
    Full,
    Diff
}