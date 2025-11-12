using FlexGuard.Core.Models;

namespace FlexGuard.Core.Abstractions
{
    public interface IFlexBackupChunkEntryStore
    {
        Task<IReadOnlyList<FlexBackupChunkEntry>> GetAllAsync(CancellationToken ct = default);
        Task<List<FlexBackupChunkEntry>> GetByBackupEntryIdAsync(string backupId, CancellationToken ct = default);
        Task<FlexBackupChunkEntry?> GetByIdAsync(string chunkEntryId, CancellationToken ct = default);

        Task InsertAsync(FlexBackupChunkEntry row, CancellationToken ct = default);
        Task UpdateAsync(FlexBackupChunkEntry row, CancellationToken ct = default);
        Task DeleteAsync(string chunkEntryId, CancellationToken ct = default);
    }
}