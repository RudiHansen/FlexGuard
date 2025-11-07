using Dapper;
using FlexGuard.Core.Abstractions;
using FlexGuard.Core.Models;
using FlexGuard.Data.Infrastructure;
using Microsoft.Data.Sqlite;
using System.Data;

namespace FlexGuard.Data.Repositories.Sqlite
{
    public sealed class SqliteFlexBackupEntryStore : IFlexBackupEntryStore
    {
        private readonly string _cs;
        private static readonly SemaphoreSlim _writeGate = new(1, 1);

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
                      SELECT BackupEntryId, JobName, DestinationBackupFolder, OperationMode, CompressionMethod,
                             Status, StatusMessage,
                             StartDateTimeUtc, EndDateTimeUtc,
                             RunTimeMs,RunTimeCollectFilesMs,
                             TotalFiles, TotalChunks, TotalBytes, TotalBytesCompressed,
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
                      SELECT BackupEntryId, JobName, DestinationBackupFolder, OperationMode, CompressionMethod,
                             Status, StatusMessage,
                             StartDateTimeUtc, EndDateTimeUtc,
                             RunTimeMs,RunTimeCollectFilesMs,
                             TotalFiles, TotalChunks, TotalBytes, TotalBytesCompressed,
                             CompressionRatio
                      FROM FlexBackupEntry
                      WHERE BackupEntryId = @BackupEntryId;
                      """;

            return await conn.QuerySingleOrDefaultAsync<FlexBackupEntry>(
                new CommandDefinition(sql, new { BackupEntryId = backupEntryId }, cancellationToken: ct));
        }

        public async Task<List<FlexBackupEntry>?> GetByJobNameAsync(string jobName, CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            using var conn = await OpenAsync(ct);

            var sql = """
                      SELECT BackupEntryId, JobName, DestinationBackupFolder, OperationMode, CompressionMethod,
                             Status, StatusMessage,
                             StartDateTimeUtc, EndDateTimeUtc,
                             RunTimeMs,RunTimeCollectFilesMs,
                             TotalFiles, TotalChunks, TotalBytes, TotalBytesCompressed,
                             CompressionRatio
                      FROM FlexBackupEntry
                      WHERE JobName = @JobName
                      ORDER BY StartDateTimeUtc DESC;
                      """;

            var rows = await conn.QueryAsync<FlexBackupEntry>(
                new CommandDefinition(sql, new { JobName = jobName }, cancellationToken: ct));
            return rows.ToList();
        }

        public async Task<DateTimeOffset?> GetLastJobRunTime(string jobName, CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            using var conn = await OpenAsync(ct);

            var sql = """
              SELECT StartDateTimeUtc
              FROM FlexBackupEntry
              WHERE JobName = @JobName
              ORDER BY StartDateTimeUtc DESC
              LIMIT 1;
              """;

            var result = await conn.QueryFirstOrDefaultAsync<DateTimeOffset?>(
                new CommandDefinition(sql, new { JobName = jobName }, cancellationToken: ct));

            return result;
        }

        public async Task InsertAsync(FlexBackupEntry row, CancellationToken ct = default)
        {
            await _writeGate.WaitAsync(ct);
            try
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
                            (BackupEntryId, JobName, DestinationBackupFolder, OperationMode, CompressionMethod,
                             Status, StatusMessage,
                             StartDateTimeUtc, EndDateTimeUtc,
                             RunTimeMs,RunTimeCollectFilesMs,
                             TotalFiles, TotalChunks, TotalBytes, TotalBytesCompressed,
                             CompressionRatio)
                          VALUES
                            (@BackupEntryId, @JobName, @DestinationBackupFolder, @OperationMode, @CompressionMethod,
                             @Status, @StatusMessage,
                             @StartDateTimeUtc, @EndDateTimeUtc,
                             @RunTimeMs,@RunTimeCollectFilesMs,
                             @TotalFiles, @TotalChunks, @TotalBytes, @TotalBytesCompressed,
                             @CompressionRatio);
                          """;

                await conn.ExecuteAsync(new CommandDefinition(sql, row, cancellationToken: ct));
            }
            finally
            {
                _writeGate.Release();
            }
        }

        public async Task UpdateAsync(FlexBackupEntry row, CancellationToken ct = default)
        {
            await _writeGate.WaitAsync(ct);
            try
            {
                Validate(row);
                await EnsureSchemaAsync(ct);
                using var conn = await OpenAsync(ct);

                var sql = """
                          UPDATE FlexBackupEntry
                          SET JobName=@JobName,
                              DestinationBackupFolder=@DestinationBackupFolder,
                              OperationMode=@OperationMode,
                              CompressionMethod=@CompressionMethod,
                              Status=@Status,
                              StatusMessage=@StatusMessage,
                              StartDateTimeUtc=@StartDateTimeUtc,
                              EndDateTimeUtc=@EndDateTimeUtc,
                              RunTimeMs=@RunTimeMs,
                              RunTimeCollectFilesMs=@RunTimeCollectFilesMs,
                              TotalFiles=@TotalFiles,
                              TotalChunks=@TotalChunks,
                              TotalBytes=@TotalBytes,
                              TotalBytesCompressed=@TotalBytesCompressed,
                              CompressionRatio=@CompressionRatio
                          WHERE BackupEntryId=@BackupEntryId;
                          """;

                var n = await conn.ExecuteAsync(new CommandDefinition(sql, row, cancellationToken: ct));
                if (n == 0)
                    throw new InvalidOperationException($"BackupEntry {row.BackupEntryId} not found.");
            }
            finally
            {
                _writeGate.Release();
            }
        }

        public async Task DeleteAsync(string backupEntryId, CancellationToken ct = default)
        {
            await _writeGate.WaitAsync(ct);
            try
            {
                await EnsureSchemaAsync(ct);
                using var conn = await OpenAsync(ct);

                await conn.ExecuteAsync(new CommandDefinition(
                    "DELETE FROM FlexBackupEntry WHERE BackupEntryId=@BackupEntryId;",
                    new { BackupEntryId = backupEntryId },
                    cancellationToken: ct));
            }
            finally
            {
                _writeGate.Release();
            }
        }

        private static void Validate(FlexBackupEntry e)
        {
            EnsureMax(e.JobName, FlexBackupLimits.JobNameMax, nameof(e.JobName));
            EnsureMax(e.DestinationBackupFolder, FlexBackupLimits.DestinationBackupFolderMax, nameof(e.DestinationBackupFolder));
            EnsureMax(e.StatusMessage, FlexBackupLimits.StatusMessageMax, nameof(e.StatusMessage));
        }

        private static void EnsureMax(string? value, int max, string fieldName)
        {
            if (value is null) return;
            if (value.Length > max)
                throw new ArgumentException($"{fieldName} length must be ≤ {max} characters.", fieldName);
        }

        private async Task EnsureSchemaAsync(CancellationToken ct)
        {
            await SqliteFlexBackupSchemaHelper.EnsureSchemaAsync(_cs, ct);
        }
    }
}