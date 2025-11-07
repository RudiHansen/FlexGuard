using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;

namespace FlexGuard.Data.Repositories.Sqlite
{
    /// <summary>
    /// Provides a single shared schema initializer for all SQLite FlexBackup stores.
    /// Ensures that the database schema is created once per process and
    /// that schema initialization is thread-safe.
    /// </summary>
    public static class SqliteFlexBackupSchemaHelper
    {
        private static readonly SemaphoreSlim _schemaGate = new(1, 1);
        private static bool _schemaReady;

        /// <summary>
        /// Ensures that the FlexBackup SQLite schema exists.
        /// Creates all required tables and indexes if missing.
        /// </summary>
        /// <param name="connectionString">The SQLite connection string to use.</param>
        /// <param name="ct">Cancellation token.</param>
        public static async Task EnsureSchemaAsync(string connectionString, CancellationToken ct = default)
        {
            if (_schemaReady)
                return;

            await _schemaGate.WaitAsync(ct);
            try
            {
                if (_schemaReady)
                    return;

                using var conn = new SqliteConnection(connectionString);
                await conn.OpenAsync(ct);

                // Configure for concurrent reads/writes
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                var ddl = """
                CREATE TABLE IF NOT EXISTS FlexBackupEntry (
                  BackupEntryId             TEXT    NOT NULL PRIMARY KEY, -- ULID(26)
                  JobName                   TEXT    NOT NULL CHECK(length(JobName) <= 50),
                  DestinationBackupFolder   TEXT    NOT NULL CHECK(length(DestinationBackupFolder) <= 255),
                  OperationMode             INTEGER NOT NULL,
                  CompressionMethod         INTEGER NOT NULL,
                  Status                    INTEGER NOT NULL,
                  StatusMessage             TEXT    NULL CHECK(length(StatusMessage) <= 255),
                  StartDateTimeUtc          TEXT    NOT NULL,
                  EndDateTimeUtc            TEXT    NULL,
                  RunTimeMs                 INTEGER NOT NULL CHECK(RunTimeMs >= 0),
                  RunTimeCollectFilesMs     INTEGER NOT NULL CHECK(RunTimeCollectFilesMs >= 0),
                  TotalFiles                INTEGER NOT NULL CHECK(TotalFiles >= 0),
                  TotalChunks               INTEGER NOT NULL CHECK(TotalChunks >= 0),
                  TotalBytes                INTEGER NOT NULL CHECK(TotalBytes >= 0),
                  TotalBytesCompressed      INTEGER NOT NULL CHECK(TotalBytesCompressed >= 0),
                  CompressionRatio          REAL    NOT NULL CHECK(CompressionRatio >= 0)
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
                  ChunkHash             TEXT    NOT NULL CHECK(length(ChunkHash) <= 64),
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
                  CompressionMethod     INTEGER NOT NULL,
                  Status                INTEGER NOT NULL,
                  StatusMessage         TEXT    NULL CHECK(length(StatusMessage) <= 255),
                  StartDateTimeUtc      TEXT    NOT NULL,
                  EndDateTimeUtc        TEXT    NULL,
                  RunTimeMs             INTEGER NOT NULL CHECK(RunTimeMs >= 0),
                  CreateTimeMs          INTEGER NOT NULL CHECK(CreateTimeMs >= 0),
                  CompressTimeMs        INTEGER NOT NULL CHECK(CompressTimeMs >= 0),
                  RelativePath          TEXT    NOT NULL CHECK(length(RelativePath) <= 512),
                  LastWriteTimeUtc      TEXT    NOT NULL,
                  FileHash              TEXT    NOT NULL CHECK(length(FileHash) <= 64),
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
            finally
            {
                _schemaGate.Release();
            }
        }
    }
}
