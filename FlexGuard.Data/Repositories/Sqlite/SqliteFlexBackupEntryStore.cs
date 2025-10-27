using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using FlexGuard.Core.Abstractions;
using FlexGuard.Core.Models;
using FlexGuard.Data.Infrastructure;

namespace FlexGuard.Data.Repositories.Sqlite
{
    public sealed class SqliteFlexBackupEntryStore : IFlexBackupEntryStore
    {
        private readonly string _cs;
        private bool _schemaReady;

        static SqliteFlexBackupEntryStore()
        {
            DapperTypeHandlers.EnsureRegistered();
        }

        public SqliteFlexBackupEntryStore(string sqlitePath)
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

        public async Task<IReadOnlyList<FlexBackupEntry>> GetAllAsync(CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            using var conn = await OpenAsync(ct);

            var sql = """
                      SELECT BackupEntryId, JobName, OperationMode, CompressionMethod,
                             Status, StatusMessage,
                             StartDateTimeUtc, EndDateTimeUtc,
                             RunTimeMs,
                             TotalFiles, TotalGroups, TotalBytes, TotalBytesCompressed,
                             CompressionRatio
                      FROM FlexBackupEntry
                      ORDER BY StartDateTimeUtc DESC;
                      """;

            var rows = await conn.QueryAsync<FlexBackupEntry>(
                new CommandDefinition(sql, cancellationToken: ct));
            return rows.ToList();
        }

        public async Task<FlexBackupEntry?> GetByIdAsync(string backupEntryId, CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            using var conn = await OpenAsync(ct);

            var sql = """
                      SELECT BackupEntryId, JobName, OperationMode, CompressionMethod,
                             Status, StatusMessage,
                             StartDateTimeUtc, EndDateTimeUtc,
                             RunTimeMs,
                             TotalFiles, TotalGroups, TotalBytes, TotalBytesCompressed,
                             CompressionRatio
                      FROM FlexBackupEntry
                      WHERE BackupEntryId = @BackupEntryId;
                      """;

            return await conn.QuerySingleOrDefaultAsync<FlexBackupEntry>(
                new CommandDefinition(sql, new { BackupEntryId = backupEntryId }, cancellationToken: ct));
        }

        public async Task InsertAsync(FlexBackupEntry row, CancellationToken ct = default)
        {
            Validate(row);
            await EnsureSchemaAsync(ct);
            using var conn = await OpenAsync(ct);

            var exists = await conn.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    "SELECT 1 FROM FlexBackupEntry WHERE BackupEntryId=@BackupEntryId;",
                    new { row.BackupEntryId },
                    cancellationToken: ct));

            if (exists == 1)
                throw new InvalidOperationException($"BackupEntry {row.BackupEntryId} already exists.");

            var sql = """
                      INSERT INTO FlexBackupEntry
                        (BackupEntryId, JobName, OperationMode, CompressionMethod,
                         Status, StatusMessage,
                         StartDateTimeUtc, EndDateTimeUtc,
                         RunTimeMs,
                         TotalFiles, TotalGroups, TotalBytes, TotalBytesCompressed,
                         CompressionRatio)
                      VALUES
                        (@BackupEntryId, @JobName, @OperationMode, @CompressionMethod,
                         @Status, @StatusMessage,
                         @StartDateTimeUtc, @EndDateTimeUtc,
                         @RunTimeMs,
                         @TotalFiles, @TotalGroups, @TotalBytes, @TotalBytesCompressed,
                         @CompressionRatio);
                      """;

            await conn.ExecuteAsync(new CommandDefinition(sql, row, cancellationToken: ct));
        }

        public async Task UpdateAsync(FlexBackupEntry row, CancellationToken ct = default)
        {
            Validate(row);
            await EnsureSchemaAsync(ct);
            using var conn = await OpenAsync(ct);

            var sql = """
                      UPDATE FlexBackupEntry
                      SET JobName=@JobName,
                          OperationMode=@OperationMode,
                          CompressionMethod=@CompressionMethod,
                          Status=@Status,
                          StatusMessage=@StatusMessage,
                          StartDateTimeUtc=@StartDateTimeUtc,
                          EndDateTimeUtc=@EndDateTimeUtc,
                          RunTimeMs=@RunTimeMs,
                          TotalFiles=@TotalFiles,
                          TotalGroups=@TotalGroups,
                          TotalBytes=@TotalBytes,
                          TotalBytesCompressed=@TotalBytesCompressed,
                          CompressionRatio=@CompressionRatio
                      WHERE BackupEntryId=@BackupEntryId;
                      """;

            var n = await conn.ExecuteAsync(new CommandDefinition(sql, row, cancellationToken: ct));
            if (n == 0)
                throw new InvalidOperationException($"BackupEntry {row.BackupEntryId} not found.");
        }

        public async Task DeleteAsync(string backupEntryId, CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            using var conn = await OpenAsync(ct);

            await conn.ExecuteAsync(new CommandDefinition(
                "DELETE FROM FlexBackupEntry WHERE BackupEntryId=@BackupEntryId;",
                new { BackupEntryId = backupEntryId },
                cancellationToken: ct));
        }

        private static void Validate(FlexBackupEntry e)
        {
            if (string.IsNullOrWhiteSpace(e.JobName) || e.JobName.Length > FlexBackupLimits.JobNameMax)
                throw new ArgumentException($"JobName must be 1–{FlexBackupLimits.JobNameMax} chars.", nameof(e));

            if (e.StatusMessage is { Length: > FlexBackupLimits.StatusMessageMax })
                throw new ArgumentException($"StatusMessage must be ≤ {FlexBackupLimits.StatusMessageMax} chars.", nameof(e));

            if (e.TotalFiles < 0 || e.TotalGroups < 0 || e.TotalBytes < 0 || e.TotalBytesCompressed < 0)
                throw new ArgumentOutOfRangeException(nameof(e), "Totals must be >= 0.");

            if (e.RunTimeMs < 0)
                throw new ArgumentOutOfRangeException(nameof(e), "RunTimeMs must be >= 0.");

            if (e.CompressionRatio < 0)
                throw new ArgumentOutOfRangeException(nameof(e), "CompressionRatio must be >= 0.");
        }

        private async Task EnsureSchemaAsync(CancellationToken ct)
        {
            if (_schemaReady) return;
            using var conn = await OpenAsync(ct);

            var ddl = """
            CREATE TABLE IF NOT EXISTS FlexBackupEntry (
              BackupEntryId         TEXT    NOT NULL PRIMARY KEY, -- ULID(26)
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