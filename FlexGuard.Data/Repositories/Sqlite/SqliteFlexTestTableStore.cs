using Microsoft.Data.Sqlite;
using System.Data;
using Dapper;
using FlexGuard.Core.Abstractions;
using FlexGuard.Core.Models;

namespace FlexGuard.Data.Repositories.Sqlite;

public sealed class SqliteFlexTestTableStore : IFlexTestTableStore
{
    private readonly string _cs;

    public SqliteFlexTestTableStore(string sqlitePath)
    {
        _cs = $"Data Source={sqlitePath};Cache=Shared;";
    }

    private async Task<IDbConnection> OpenAsync(CancellationToken ct)
    {
        var conn = new SqliteConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
        await cmd.ExecuteNonQueryAsync(ct);
        return conn;
    }

    public async Task<IReadOnlyList<FlexTestRow>> GetAllAsync(CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var conn = await OpenAsync(ct);
        var rows = await conn.QueryAsync<FlexTestRow>(
            new CommandDefinition("SELECT Id, TestNavn FROM FlexTestTable ORDER BY Id;", cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<FlexTestRow?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var conn = await OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<FlexTestRow>(
            new CommandDefinition("SELECT Id, TestNavn FROM FlexTestTable WHERE Id=@Id;", new { Id = id }, cancellationToken: ct));
    }
    public async Task InsertAsync(FlexTestRow row, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(row.TestNavn) || row.TestNavn.Length > DomainLimits.TestNavnMax)
            throw new ArgumentException($"'{nameof(row.TestNavn)}' must be ≤ {DomainLimits.TestNavnMax} characters.", nameof(row));

        await EnsureSchemaAsync(ct);
        using var conn = await OpenAsync(ct);

        // Tjek eksistens for tydelig fejl
        var exists = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT 1 FROM FlexTestTable WHERE Id=@Id;", new { row.Id }, cancellationToken: ct));
        if (exists == 1)
            throw new InvalidOperationException($"Row with Id={row.Id} already exists.");

        const string sql = "INSERT INTO FlexTestTable (Id, TestNavn) VALUES (@Id, @TestNavn);";
        await conn.ExecuteAsync(new CommandDefinition(sql, row, cancellationToken: ct));
    }

    public async Task UpdateAsync(FlexTestRow row, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(row.TestNavn) || row.TestNavn.Length > DomainLimits.TestNavnMax)
            throw new ArgumentException($"'{nameof(row.TestNavn)}' must be ≤ {DomainLimits.TestNavnMax} characters.", nameof(row));

        await EnsureSchemaAsync(ct);
        using var conn = await OpenAsync(ct);

        const string sql = "UPDATE FlexTestTable SET TestNavn=@TestNavn WHERE Id=@Id;";
        var affected = await conn.ExecuteAsync(new CommandDefinition(sql, row, cancellationToken: ct));
        if (affected == 0)
            throw new InvalidOperationException($"Row with Id={row.Id} was not found.");
    }
    public async Task UpsertAsync(FlexTestRow row, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(row.TestNavn) || row.TestNavn.Length > DomainLimits.TestNavnMax)
            throw new ArgumentException($"'{nameof(row.TestNavn)}' must be ≤ {DomainLimits.TestNavnMax} characters.", nameof(row));

        await EnsureSchemaAsync(ct);
        using var conn = await OpenAsync(ct);
        var sql = """
            INSERT INTO FlexTestTable (Id, TestNavn)
            VALUES (@Id, @TestNavn)
            ON CONFLICT(Id) DO UPDATE SET TestNavn = excluded.TestNavn;
            """;
        await conn.ExecuteAsync(new CommandDefinition(sql, row, cancellationToken: ct));
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var conn = await OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM FlexTestTable WHERE Id=@Id;", new { Id = id }, cancellationToken: ct));
    }

    private bool _schemaReady;
    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        if (_schemaReady) return;
        using var conn = await OpenAsync(ct);
        var ddl = """
            CREATE TABLE IF NOT EXISTS FlexTestTable (
              Id        INTEGER PRIMARY KEY,
              TestNavn  TEXT NOT NULL CHECK(length(TestNavn) <= 20)
            );
            """;
        await conn.ExecuteAsync(new CommandDefinition(ddl, cancellationToken: ct));
        _schemaReady = true;
    }
}