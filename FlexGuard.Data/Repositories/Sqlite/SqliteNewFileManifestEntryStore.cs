using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using FlexGuard.Core.Abstractions;
using FlexGuard.Core.Models;

namespace FlexGuard.Data.Repositories.Sqlite;

public sealed class SqliteNewFileManifestEntryStore : INewFileManifestEntryStore
{
    private readonly string _cs;
    private bool _schemaReady;

    public SqliteNewFileManifestEntryStore(string sqlitePath)
        => _cs = $"Data Source={sqlitePath};Cache=Shared;";

    private async Task<IDbConnection> OpenAsync(CancellationToken ct)
    {
        var conn = new SqliteConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
        await cmd.ExecuteNonQueryAsync(ct);
        return conn;
    }

    public async Task<IReadOnlyList<NewFileManifestEntry>> GetAllAsync(CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var conn = await OpenAsync(ct);
        var sql = """
                  SELECT Id, ManifestId, RelativePath, ChunkFile, FileSize, LastWriteTimeUtc,
                         Hash, CompressionSkipped, CompressionRatio
                  FROM NewFileManifestEntry
                  ORDER BY ManifestId, RelativePath;
                  """;
        var rows = await conn.QueryAsync<NewFileManifestEntry>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<NewFileManifestEntry?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var conn = await OpenAsync(ct);
        var sql = """
                  SELECT Id, ManifestId, RelativePath, ChunkFile, FileSize, LastWriteTimeUtc,
                         Hash, CompressionSkipped, CompressionRatio
                  FROM NewFileManifestEntry WHERE Id=@Id;
                  """;
        return await conn.QuerySingleOrDefaultAsync<NewFileManifestEntry>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    public async Task InsertAsync(NewFileManifestEntry row, CancellationToken ct = default)
    {
        Validate(row);
        await EnsureSchemaAsync(ct);
        using var conn = await OpenAsync(ct);

        var exists = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT 1 FROM NewFileManifestEntry WHERE Id=@Id;", new { row.Id }, cancellationToken: ct));
        if (exists == 1) throw new InvalidOperationException($"Entry {row.Id} already exists.");

        var sql = """
                  INSERT INTO NewFileManifestEntry
                    (Id, ManifestId, RelativePath, ChunkFile, FileSize, LastWriteTimeUtc,
                     Hash, CompressionSkipped, CompressionRatio)
                  VALUES
                    (@Id, @ManifestId, @RelativePath, @ChunkFile, @FileSize, @LastWriteTimeUtc,
                     @Hash, @CompressionSkipped, @CompressionRatio);
                  """;
        await conn.ExecuteAsync(new CommandDefinition(sql, row, cancellationToken: ct));
    }

    public async Task UpdateAsync(NewFileManifestEntry row, CancellationToken ct = default)
    {
        Validate(row);
        await EnsureSchemaAsync(ct);
        using var conn = await OpenAsync(ct);

        var sql = """
                  UPDATE NewFileManifestEntry
                  SET ManifestId=@ManifestId,
                      RelativePath=@RelativePath,
                      ChunkFile=@ChunkFile,
                      FileSize=@FileSize,
                      LastWriteTimeUtc=@LastWriteTimeUtc,
                      Hash=@Hash,
                      CompressionSkipped=@CompressionSkipped,
                      CompressionRatio=@CompressionRatio
                  WHERE Id=@Id;
                  """;
        var n = await conn.ExecuteAsync(new CommandDefinition(sql, row, cancellationToken: ct));
        if (n == 0) throw new InvalidOperationException($"Entry {row.Id} was not found.");
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var conn = await OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM NewFileManifestEntry WHERE Id=@Id;", new { Id = id }, cancellationToken: ct));
    }

    private static void Validate(NewFileManifestEntry e)
    {
        if (string.IsNullOrWhiteSpace(e.ManifestId))
            throw new ArgumentException("ManifestId is required.", nameof(e));
        if (string.IsNullOrWhiteSpace(e.RelativePath) || e.RelativePath.Length > NewFileManifestLimits.PathMax)
            throw new ArgumentException($"RelativePath must be 1–{NewFileManifestLimits.PathMax} chars.", nameof(e));
        if (string.IsNullOrWhiteSpace(e.ChunkFile) || e.ChunkFile.Length > NewFileManifestLimits.PathMax)
            throw new ArgumentException($"ChunkFile must be 1–{NewFileManifestLimits.PathMax} chars.", nameof(e));
        if (string.IsNullOrWhiteSpace(e.Hash) || e.Hash.Length != NewFileManifestLimits.HashHexLen)
            throw new ArgumentException($"Hash must be {NewFileManifestLimits.HashHexLen} hex chars.", nameof(e));
    }

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        if (_schemaReady) return;
        using var conn = await OpenAsync(ct);

        var ddl = """
        CREATE TABLE IF NOT EXISTS NewFileManifest (
          Id           TEXT    NOT NULL PRIMARY KEY,     -- ULID (26)
          JobName      TEXT    NOT NULL CHECK(length(JobName) <= 50),
          Type         INTEGER NOT NULL,                 -- ManifestType enum (int)
          TimestampUtc TEXT    NOT NULL,
          Compression  INTEGER NOT NULL,                 -- CompressionMethod enum (int)
          RunRefId     INTEGER NULL,
          CreatedUtc   TEXT    NOT NULL
        );

        CREATE TABLE IF NOT EXISTS NewFileManifestEntry (
          Id                 TEXT    NOT NULL PRIMARY KEY,     -- ULID (26)
          ManifestId         TEXT    NOT NULL,                 -- FK (logisk)
          RelativePath       TEXT    NOT NULL CHECK(length(RelativePath) <= 1024),
          ChunkFile          TEXT    NOT NULL CHECK(length(ChunkFile) <= 1024),
          FileSize           INTEGER NOT NULL,
          LastWriteTimeUtc   TEXT    NOT NULL,
          Hash               TEXT    NOT NULL CHECK(length(Hash) = 64),
          CompressionSkipped INTEGER NOT NULL DEFAULT 0,       -- bool -> 0/1
          CompressionRatio   NUMERIC NULL
        );

        CREATE INDEX IF NOT EXISTS IX_NewFileManifest_JobName_Timestamp
          ON NewFileManifest (JobName, TimestampUtc DESC);

        CREATE UNIQUE INDEX IF NOT EXISTS UX_NewFileManifestEntry_ManifestId_RelativePath
          ON NewFileManifestEntry (ManifestId, RelativePath);

        CREATE INDEX IF NOT EXISTS IX_NewFileManifestEntry_ManifestId
          ON NewFileManifestEntry (ManifestId);
        """;

        await conn.ExecuteAsync(new CommandDefinition(ddl, cancellationToken: ct));
        _schemaReady = true;
    }
}