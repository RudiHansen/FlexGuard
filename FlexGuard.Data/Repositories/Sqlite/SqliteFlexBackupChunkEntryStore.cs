using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using FlexGuard.Core.Abstractions;
using FlexGuard.Core.Models;
using FlexGuard.Data.Infrastructure;

namespace FlexGuard.Data.Repositories.Sqlite
{
    public sealed class SqliteFlexBackupChunkEntryStore : IFlexBackupChunkEntryStore
    {
        private readonly string _cs;
        private bool _schemaReady;

        static SqliteFlexBackupChunkEntryStore()
        {
            DapperTypeHandlers.EnsureRegistered();
        }

        public SqliteFlexBackupChunkEntryStore(string sqlitePath)
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

        public async Task<IReadOnlyList<FlexBackupChunkEntry>> GetAllAsync(CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            using var conn = await OpenAsync(ct);

            var sql = """
                      SELECT ChunkEntryId, BackupEntryId,
                             CompressionMethod,
                             Status, StatusMessage,
                             StartDateTimeUtc, EndDateTimeUtc,
                             RunTimeMs, CreateTimeMs, CompressTimeMs,
                             ChunkFileName, ChunkHash,
                             FileSize, FileSizeCompressed,
                             CpuTimeMs, CpuPercent,
                             MemoryStart, MemoryEnd
                      FROM FlexBackupChunkEntry
                      ORDER BY BackupEntryId, ChunkEntryId;
                      """;

            var rows = await conn.QueryAsync<FlexBackupChunkEntry>(
                new CommandDefinition(sql, cancellationToken: ct));
            return rows.ToList();
        }

        public async Task<FlexBackupChunkEntry?> GetByIdAsync(string chunkEntryId, CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            using var conn = await OpenAsync(ct);

            var sql = """
                      SELECT ChunkEntryId, BackupEntryId,
                             CompressionMethod,
                             Status, StatusMessage,
                             StartDateTimeUtc, EndDateTimeUtc,
                             RunTimeMs, CreateTimeMs, CompressTimeMs,
                             ChunkFileName, ChunkHash,
                             FileSize, FileSizeCompressed,
                             CpuTimeMs, CpuPercent,
                             MemoryStart, MemoryEnd
                      FROM FlexBackupChunkEntry
                      WHERE ChunkEntryId=@ChunkEntryId;
                      """;

            return await conn.QuerySingleOrDefaultAsync<FlexBackupChunkEntry>(
                new CommandDefinition(sql, new { ChunkEntryId = chunkEntryId }, cancellationToken: ct));
        }

        public async Task InsertAsync(FlexBackupChunkEntry row, CancellationToken ct = default)
        {
            Validate(row);
            await EnsureSchemaAsync(ct);
            using var conn = await OpenAsync(ct);

            var exists = await conn.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    "SELECT 1 FROM FlexBackupChunkEntry WHERE ChunkEntryId=@ChunkEntryId;",
                    new { row.ChunkEntryId },
                    cancellationToken: ct));

            if (exists == 1)
                throw new InvalidOperationException($"ChunkEntry {row.ChunkEntryId} already exists.");

            var sql = """
                      INSERT INTO FlexBackupChunkEntry
                        (ChunkEntryId, BackupEntryId,
                         CompressionMethod,
                         Status, StatusMessage,
                         StartDateTimeUtc, EndDateTimeUtc,
                         RunTimeMs, CreateTimeMs, CompressTimeMs,
                         ChunkFileName, ChunkHash,
                         FileSize, FileSizeCompressed,
                         CpuTimeMs, CpuPercent,
                         MemoryStart, MemoryEnd)
                      VALUES
                        (@ChunkEntryId, @BackupEntryId,
                         @CompressionMethod,
                         @Status, @StatusMessage,
                         @StartDateTimeUtc, @EndDateTimeUtc,
                         @RunTimeMs, @CreateTimeMs, @CompressTimeMs,
                         @ChunkFileName, @ChunkHash,
                         @FileSize, @FileSizeCompressed,
                         @CpuTimeMs, @CpuPercent,
                         @MemoryStart, @MemoryEnd);
                      """;

            await conn.ExecuteAsync(new CommandDefinition(sql, row, cancellationToken: ct));
        }

        public async Task UpdateAsync(FlexBackupChunkEntry row, CancellationToken ct = default)
        {
            Validate(row);
            await EnsureSchemaAsync(ct);
            using var conn = await OpenAsync(ct);

            var sql = """
                      UPDATE FlexBackupChunkEntry
                      SET BackupEntryId=@BackupEntryId,
                          CompressionMethod=@CompressionMethod,
                          Status=@Status,
                          StatusMessage=@StatusMessage,
                          StartDateTimeUtc=@StartDateTimeUtc,
                          EndDateTimeUtc=@EndDateTimeUtc,
                          RunTimeMs=@RunTimeMs,
                          CreateTimeMs=@CreateTimeMs,
                          CompressTimeMs=@CompressTimeMs,
                          ChunkFileName=@ChunkFileName,
                          ChunkHash=@ChunkHash,
                          FileSize=@FileSize,
                          FileSizeCompressed=@FileSizeCompressed,
                          CpuTimeMs=@CpuTimeMs,
                          CpuPercent=@CpuPercent,
                          MemoryStart=@MemoryStart,
                          MemoryEnd=@MemoryEnd
                      WHERE ChunkEntryId=@ChunkEntryId;
                      """;

            var n = await conn.ExecuteAsync(new CommandDefinition(sql, row, cancellationToken: ct));
            if (n == 0)
                throw new InvalidOperationException($"ChunkEntry {row.ChunkEntryId} not found.");
        }

        public async Task DeleteAsync(string chunkEntryId, CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            using var conn = await OpenAsync(ct);

            await conn.ExecuteAsync(new CommandDefinition(
                "DELETE FROM FlexBackupChunkEntry WHERE ChunkEntryId=@ChunkEntryId;",
                new { ChunkEntryId = chunkEntryId },
                cancellationToken: ct));
        }

        private static void Validate(FlexBackupChunkEntry e)
        {
            EnsureMax(e.ChunkEntryId, FlexBackupLimits.UlidLen, nameof(e.ChunkEntryId));
            EnsureMax(e.BackupEntryId, FlexBackupLimits.UlidLen, nameof(e.BackupEntryId));
            EnsureMax(e.ChunkFileName, FlexBackupLimits.ChunkFileNameMax, nameof(e.ChunkFileName));
            EnsureMax(e.ChunkHash, FlexBackupLimits.HashHexLen, nameof(e.ChunkHash));
            EnsureMax(e.StatusMessage, FlexBackupLimits.StatusMessageMax, nameof(e.StatusMessage));
        }
        private static void EnsureMax(string? value, int max, string fieldName)
        {
            if (value is null) return;            // null er ok
            if (value.Length > max)
                throw new ArgumentException($"{fieldName} length must be ≤ {max} characters.", fieldName);
        }
        private async Task EnsureSchemaAsync(CancellationToken ct)
        {
            if (_schemaReady) return;
            using var conn = await OpenAsync(ct);

            var ddl = """
            CREATE TABLE IF NOT EXISTS FlexBackupEntry (
              BackupEntryId         TEXT    NOT NULL PRIMARY KEY,
              JobName               TEXT    NOT NULL CHECK(length(JobName) <= 50),
              OperationMode         INTEGER NOT NULL,
              CompressionMethod     INTEGER NOT NULL,
              Status                INTEGER NOT NULL,
              StatusMessage         TEXT    NULL CHECK(length(StatusMessage) <= 255),
              StartDateTimeUtc      TEXT    NOT NULL,
              EndDateTimeUtc        TEXT    NULL,
              RunTimeMs             INTEGER NOT NULL CHECK(RunTimeMs >= 0),
              TotalFiles            INTEGER NOT NULL CHECK(TotalFiles >= 0),
              TotalGroups           INTEGER NOT NULL CHECK(TotalGroups >= 0),
              TotalBytes            INTEGER NOT NULL CHECK(TotalBytes >= 0),
              TotalBytesCompressed  INTEGER NOT NULL CHECK(TotalBytesCompressed >= 0),
              CompressionRatio      REAL    NOT NULL CHECK(CompressionRatio >= 0)
            );

            CREATE TABLE IF NOT EXISTS FlexBackupChunkEntry (
              ChunkEntryId          TEXT    NOT NULL PRIMARY KEY,
              BackupEntryId         TEXT    NOT NULL,
              CompressionMethod     INTEGER NOT NULL,
              Status                INTEGER NOT NULL,
              StatusMessage         TEXT    NULL CHECK(length(StatusMessage) <= 255),
              StartDateTimeUtc      TEXT    NOT NULL,
              EndDateTimeUtc        TEXT    NULL,
              RunTimeMs             INTEGER NOT NULL CHECK(RunTimeMs >= 0),
              CreateTimeMs          INTEGER NOT NULL CHECK(CreateTimeMs >= 0),
              CompressTimeMs        INTEGER NOT NULL CHECK(CompressTimeMs >= 0),
              ChunkFileName         TEXT    NOT NULL CHECK(length(ChunkFileName) <= 50),
              ChunkHash             TEXT    NOT NULL CHECK(length(ChunkHash) = 64),
              FileSize              INTEGER NOT NULL CHECK(FileSize >= 0),
              FileSizeCompressed    INTEGER NOT NULL CHECK(FileSizeCompressed >= 0),
              CpuTimeMs             INTEGER NOT NULL CHECK(CpuTimeMs >= 0),
              CpuPercent            REAL    NOT NULL CHECK(CpuPercent >= 0),
              MemoryStart           INTEGER NOT NULL,
              MemoryEnd             INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS FlexBackupFileEntry (
              FileEntryId           TEXT    NOT NULL PRIMARY KEY,
              ChunkEntryId          TEXT    NOT NULL,
              BackupEntryId         TEXT    NOT NULL,
              Status                INTEGER NOT NULL,
              StatusMessage         TEXT    NULL CHECK(length(StatusMessage) <= 255),
              StartDateTimeUtc      TEXT    NOT NULL,
              EndDateTimeUtc        TEXT    NULL,
              RunTimeMs             INTEGER NOT NULL CHECK(RunTimeMs >= 0),
              CreateTimeMs          INTEGER NOT NULL CHECK(CreateTimeMs >= 0),
              CompressTimeMs        INTEGER NOT NULL CHECK(CompressTimeMs >= 0),
              RelativePath          TEXT    NOT NULL CHECK(length(RelativePath) <= 512),
              LastWriteTimeUtc      TEXT    NOT NULL,
              FileHash              TEXT    NOT NULL CHECK(length(FileHash) = 64),
              FileSize              INTEGER NOT NULL CHECK(FileSize >= 0),
              FileSizeCompressed    INTEGER NOT NULL CHECK(FileSizeCompressed >= 0),
              CpuTimeMs             INTEGER NOT NULL CHECK(CpuTimeMs >= 0),
              CpuPercent            REAL    NOT NULL CHECK(CpuPercent >= 0),
              MemoryStart           INTEGER NOT NULL,
              MemoryEnd             INTEGER NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_FlexBackupChunkEntry_BackupEntryId
              ON FlexBackupChunkEntry (BackupEntryId);

            CREATE INDEX IF NOT EXISTS IX_FlexBackupFileEntry_BackupEntryId
              ON FlexBackupFileEntry (BackupEntryId);

            CREATE INDEX IF NOT EXISTS IX_FlexBackupFileEntry_ChunkEntryId
              ON FlexBackupFileEntry (ChunkEntryId);
            """;

            await conn.ExecuteAsync(new CommandDefinition(ddl, cancellationToken: ct));
            _schemaReady = true;
        }
    }
}