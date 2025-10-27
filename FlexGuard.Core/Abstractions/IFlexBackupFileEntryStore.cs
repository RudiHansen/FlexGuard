using FlexGuard.Core.Models;

namespace FlexGuard.Core.Abstractions
{
    public interface IFlexBackupFileEntryStore
    {
        Task<IReadOnlyList<FlexBackupFileEntry>> GetAllAsync(CancellationToken ct = default);
        Task<FlexBackupFileEntry?> GetByIdAsync(string fileEntryId, CancellationToken ct = default);

        Task InsertAsync(FlexBackupFileEntry row, CancellationToken ct = default);
        Task UpdateAsync(FlexBackupFileEntry row, CancellationToken ct = default);
        Task DeleteAsync(string fileEntryId, CancellationToken ct = default);
    }
}