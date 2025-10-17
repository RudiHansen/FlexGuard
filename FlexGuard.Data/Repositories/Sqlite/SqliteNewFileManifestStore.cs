using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using FlexGuard.Core.Abstractions;
using FlexGuard.Core.Models;

namespace FlexGuard.Data.Repositories.Sqlite;

public sealed class SqliteNewFileManifestStore : INewFileManifestStore
{
    private readonly string _cs;
    private bool _schemaReady;

    public SqliteNewFileManifestStore(string sqlitePath)
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

    public async Task<IReadOnlyList<NewFileManifest>> GetAllAsync(CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var conn = await OpenAsync(ct);
        var sql = """
                  SELECT Id, JobName, Type, TimestampUtc, Compression, RunRefId, CreatedUtc
                  FROM NewFileManifest ORDER BY TimestampUtc DESC;
                  """;
        var rows = await conn.QueryAsync<NewFileManifest>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<NewFileManifest?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var conn = await OpenAsync(ct);
        var sql = """
                  SELECT Id, JobName, Type, TimestampUtc, Compression, RunRefId, CreatedUtc
                  FROM NewFileManifest WHERE Id=@Id;
                  """;
        return await conn.QuerySingleOrDefaultAsync<NewFileManifest>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    public async Task InsertAsync(NewFileManifest row, CancellationToken ct = default)
    {
        Validate(row);
        await EnsureSchemaAsync(ct);
        using var conn = await OpenAsync(ct);

        var exists = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT 1 FROM NewFileManifest WHERE Id=@Id;", new { row.Id }, cancellationToken: ct));
        if (exists == 1) throw new InvalidOperationException($"Manifest {row.Id} already exists.");

        var sql = """
                  INSERT INTO NewFileManifest
                    (Id, JobName, Type, TimestampUtc, Compression, RunRefId, CreatedUtc)
                  VALUES
                    (@Id, @JobName, @Type, @TimestampUtc, @Compression, @RunRefId, @CreatedUtc);
                  """;
        await conn.ExecuteAsync(new CommandDefinition(sql, row, cancellationToken: ct));
    }

    public async Task UpdateAsync(NewFileManifest row, CancellationToken ct = default)
    {
        Validate(row);
        await EnsureSchemaAsync(ct);
        using var conn = await OpenAsync(ct);

        var sql = """
                  UPDATE NewFileManifest
                  SET JobName=@JobName,
                      Type=@Type,
                      TimestampUtc=@TimestampUtc,
                      Compression=@Compression,
                      RunRefId=@RunRefId,
                      CreatedUtc=@CreatedUtc
                  WHERE Id=@Id;
                  """;
        var n = await conn.ExecuteAsync(new CommandDefinition(sql, row, cancellationToken: ct));
        if (n == 0) throw new InvalidOperationException($"Manifest {row.Id} was not found.");
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var conn = await OpenAsync(ct);
        // NB: sletter kun header her; entries håndteres i entry-store eller via FK-cascade hvis sat op
        await conn.ExecuteAsync(new CommandDefinition("DELETE FROM NewFileManifest WHERE Id=@Id;", new { Id = id }, cancellationToken: ct));
    }

    private static void Validate(NewFileManifest m)
    {
        if (string.IsNullOrWhiteSpace(m.JobName) || m.JobName.Length > NewFileManifestLimits.JobNameMax)
            throw new ArgumentException($"JobName must be 1–{NewFileManifestLimits.JobNameMax} chars.", nameof(m));
        // Type/Compression er enums (lagres som INTEGER), tidsfelter er DateTimeOffset (TEXT) – fint.
    }

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        if (_schemaReady) return;
        using var conn = await OpenAsync(ct);

        var ddl = """
        CREATE TABLE IF NOT EXISTS NewFileManifest (
          Id           TEXT    NOT NULL PRIMARY KEY,                      -- ULID (26)
          JobName      TEXT    NOT NULL CHECK(length(JobName) <= 50),
          Type         INTEGER NOT NULL,                                  -- ManifestType enum (int)
          TimestampUtc TEXT    NOT NULL,                                  -- ISO-8601
          Compression  INTEGER NOT NULL,                                  -- CompressionMethod enum (int)
          RunRefId     INTEGER NULL,                                      -- legacy FK hvis du har en BIGINT Run
          CreatedUtc   TEXT    NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_NewFileManifest_JobName_Timestamp
          ON NewFileManifest (JobName, TimestampUtc DESC);
        """;

        await conn.ExecuteAsync(new CommandDefinition(ddl, cancellationToken: ct));
        _schemaReady = true;
    }
}
