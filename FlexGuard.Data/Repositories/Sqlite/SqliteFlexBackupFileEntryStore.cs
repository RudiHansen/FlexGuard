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

        public async Task<List<FlexBackupFileEntry>?> GetBybackupEntryIdAsync(string backupEntryId, CancellationToken ct = default)
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
                      WHERE backupEntryId=@backupEntryId;
                      """;

            var rows = await conn.QueryAsync<FlexBackupFileEntry>(
                new CommandDefinition(sql, new { backupEntryId }, cancellationToken: ct));
            return rows.ToList();
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
                        (FileEntryId, ChunkEntryId, BackupEntryId,CompressionMethod,
                         Status, StatusMessage,
                         StartDateTimeUtc, EndDateTimeUtc,
                         RunTimeMs, CreateTimeMs, CompressTimeMs,
                         RelativePath, LastWriteTimeUtc,
                         FileHash,
                         FileSize, FileSizeCompressed,
                         CpuTimeMs, CpuPercent,
                         MemoryStart, MemoryEnd)
                      VALUES
                        (@FileEntryId, @ChunkEntryId, @BackupEntryId, @CompressionMethod,
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
            EnsureMax(e.ChunkEntryId, FlexBackupLimits.UlidLen, nameof(e.ChunkEntryId));
            EnsureMax(e.BackupEntryId, FlexBackupLimits.UlidLen, nameof(e.BackupEntryId));
            EnsureMax(e.RelativePath, FlexBackupLimits.RelativePathMax, nameof(e.RelativePath));
            EnsureMax(e.FileHash, FlexBackupLimits.HashHexLen, nameof(e.FileHash));
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
            await SqliteFlexBackupSchemaHelper.EnsureSchemaAsync(_cs, ct);
        }
    }
}