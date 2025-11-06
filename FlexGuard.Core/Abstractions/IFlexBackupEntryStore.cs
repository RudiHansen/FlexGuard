using FlexGuard.Core.Models;

namespace FlexGuard.Core.Abstractions
{
    public interface IFlexBackupEntryStore
    {
        Task<IReadOnlyList<FlexBackupEntry>> GetAllAsync(CancellationToken ct = default);
        Task<FlexBackupEntry?> GetByIdAsync(string backupEntryId, CancellationToken ct = default);
        Task<List<FlexBackupEntry>?> GetByJobNameAsync(string jobName, CancellationToken ct = default);

        Task<DateTimeOffset?> GetLastJobRunTime(string jobName, CancellationToken ct = default);

        Task InsertAsync(FlexBackupEntry row, CancellationToken ct = default);
        Task UpdateAsync(FlexBackupEntry row, CancellationToken ct = default);
        Task DeleteAsync(string backupEntryId, CancellationToken ct = default);
    }
}