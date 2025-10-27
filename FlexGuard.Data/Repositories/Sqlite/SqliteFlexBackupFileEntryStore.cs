using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using FlexGuard.Core.Abstractions;
using FlexGuard.Core.Models;
using FlexGuard.Data.Infrastructure;

namespace FlexGuard.Data.Repositories.Sqlite
{
    public sealed class SqliteFlexBackupFileEntryStore : IFlexBackupFileEntryStore
    {
        private readonly string _cs;
        private bool _schemaReady;

        static SqliteFlexBackupFileEntryStore()
        {
            DapperTypeHandlers.EnsureRegistered();
        }

        public SqliteFlexBackupFileEntryStore(string sqlitePath)
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

        public async Task<IReadOnlyList<FlexBackupFileEntry>> GetAllAsync(CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            using var conn = await OpenAsync(ct);

            var sql = """
                      SELECT FileEntryId, ChunkEntryId, BackupEntryId,
                             Status, StatusMessage,
                             StartDateTimeUtc, EndDateTimeUtc,
                             RunTimeMs, CreateTimeMs, CompressTimeMs,
                             RelativePath, LastWriteTimeUtc,
                             FileHash,
                             FileSize, FileSizeCompressed,
                             CpuTimeMs, CpuPercent,
                             MemoryStart, MemoryEnd
                      FROM FlexBackupFileEntry
                      ORDER BY BackupEntryId, ChunkEntryId, RelativePath;
                      """;

            var rows = await conn.QueryAsync<FlexBackupFileEntry>(
                new CommandDefinition(sql, cancellationToken: ct));
            return rows.ToList();
        }

        public async Task<FlexBackupFileEntry?> GetByIdAsync(string fileEntryId, CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            using var conn = await OpenAsync(ct);

            var sql = """
                      SELECT FileEntryId, ChunkEntryId, BackupEntryId,
                             Status, StatusMessage,
                             StartDateTimeUtc, EndDateTimeUtc,
                             RunTimeMs, CreateTimeMs, CompressTimeMs,
                             RelativePath, LastWriteTimeUtc,
                             FileHash,
                             FileSize, FileSizeCompressed,
                             CpuTimeMs, CpuPercent,
                             MemoryStart, MemoryEnd
                      FROM FlexBackupFileEntry
                      WHERE FileEntryId=@FileEntryId;
                      """;

            return await conn.QuerySingleOrDefaultAsync<FlexBackupFileEntry>(
                new CommandDefinition(sql, new { FileEntryId = fileEntryId }, cancellationToken: ct));
        }

        public async Task InsertAsync(FlexBackupFileEntry row, CancellationToken ct = default)
        {
            Validate(row);
            await EnsureSchemaAsync(ct);
            using var conn = await OpenAsync(ct);

            var exists = await conn.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    "SELECT 1 FROM FlexBackupFileEntry WHERE FileEntryId=@FileEntryId;",
                    new { row.FileEntryId },
                    cancellationToken: ct));

            if (exists == 1)
                throw new InvalidOperationException($"FileEntry {row.FileEntryId} already exists.");

            var sql = """
                      INSERT INTO FlexBackupFileEntry
                        (FileEntryId, ChunkEntryId, BackupEntryId,
                         Status, StatusMessage,
                         StartDateTimeUtc, EndDateTimeUtc,
                         RunTimeMs, CreateTimeMs, CompressTimeMs,
                         RelativePath, LastWriteTimeUtc,
                         FileHash,
                         FileSize, FileSizeCompressed,
                         CpuTimeMs, CpuPercent,
                         MemoryStart, MemoryEnd)
                      VALUES
                        (@FileEntryId, @ChunkEntryId, @BackupEntryId,
                         @Status, @StatusMessage,
                         @StartDateTimeUtc, @EndDateTimeUtc,
                         @RunTimeMs, @CreateTimeMs, @CompressTimeMs,
                         @RelativePath, @LastWriteTimeUtc,
                         @FileHash,
                         @FileSize, @FileSizeCompressed,
                         @CpuTimeMs, @CpuPercent,
                         @MemoryStart, @MemoryEnd);
                      """;

            await conn.ExecuteAsync(new CommandDefinition(sql, row, cancellationToken: ct));
        }

        public async Task UpdateAsync(FlexBackupFileEntry row, CancellationToken ct = default)
        {
            Validate(row);
            await EnsureSchemaAsync(ct);
            using var conn = await OpenAsync(ct);

            var sql = """
                      UPDATE FlexBackupFileEntry
                      SET ChunkEntryId=@ChunkEntryId,
                          BackupEntryId=@BackupEntryId,
                          Status=@Status,
                          StatusMessage=@StatusMessage,
                          StartDateTimeUtc=@StartDateTimeUtc,
                          EndDateTimeUtc=@EndDateTimeUtc,
                          RunTimeMs=@RunTimeMs,
                          CreateTimeMs=@CreateTimeMs,
                          CompressTimeMs=@CompressTimeMs,
                          RelativePath=@RelativePath,
                          LastWriteTimeUtc=@LastWriteTimeUtc,
                          FileHash=@FileHash,
                          FileSize=@FileSize,
                          FileSizeCompressed=@FileSizeCompressed,
                          CpuTimeMs=@CpuTimeMs,
                          CpuPercent=@CpuPercent,
                          MemoryStart=@MemoryStart,
                          MemoryEnd=@MemoryEnd
                      WHERE FileEntryId=@FileEntryId;
                      """;

            var n = await conn.ExecuteAsync(new CommandDefinition(sql, row, cancellationToken: ct));
            if (n == 0)
                throw new InvalidOperationException($"FileEntry {row.FileEntryId} not found.");
        }

        public async Task DeleteAsync(string fileEntryId, CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            using var conn = await OpenAsync(ct);

            await conn.ExecuteAsync(new CommandDefinition(
                "DELETE FROM FlexBackupFileEntry WHERE FileEntryId=@FileEntryId;",
                new { FileEntryId = fileEntryId },
                cancellationToken: ct));
        }

        private static void Validate(FlexBackupFileEntry e)
        {
            if (string.IsNullOrWhiteSpace(e.ChunkEntryId))
            {
                throw new ArgumentException(
                    "ChunkEntryId is required.",
                    nameof(e));
            }

            if (string.IsNullOrWhiteSpace(e.BackupEntryId))
            {
                throw new ArgumentException(
                    "BackupEntryId is required.",
                    nameof(e));
            }

            if (string.IsNullOrWhiteSpace(e.RelativePath) ||
                e.RelativePath.Length > FlexBackupLimits.RelativePathMax)
            {
                throw new ArgumentException(
                    $"RelativePath must be 1–{FlexBackupLimits.RelativePathMax} chars.",
                    nameof(e));
            }

            if (string.IsNullOrWhiteSpace(e.FileHash) ||
                e.FileHash.Length != FlexBackupLimits.HashHexLen)
            {
                throw new ArgumentException(
                    $"FileHash must be {FlexBackupLimits.HashHexLen} hex chars.",
                    nameof(e));
            }

            if (e.StatusMessage is { Length: > FlexBackupLimits.StatusMessageMax })
            {
                throw new ArgumentException(
                    $"StatusMessage must be ≤ {FlexBackupLimits.StatusMessageMax} chars.",
                    nameof(e));
            }

            if (e.RunTimeMs < 0 ||
                e.CreateTimeMs < 0 ||
                e.CompressTimeMs < 0 ||
                e.CpuTimeMs < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(e),
                    "Timing values must be >= 0.");
            }

            if (e.FileSize < 0 ||
                e.FileSizeCompressed < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(e),
                    "FileSize values must be >= 0.");
            }

            if (e.CpuPercent < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(e),
                    "CpuPercent must be >= 0.");
            }
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
              RelativePath          TEXT    NOT NULL CHECK(length(RelativePath) <= 255),
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