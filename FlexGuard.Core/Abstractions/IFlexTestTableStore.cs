using FlexGuard.Core.Models;

namespace FlexGuard.Core.Abstractions;

public interface IFlexTestTableStore
{
    Task<IReadOnlyList<FlexTestRow>> GetAllAsync(CancellationToken ct = default);
    Task<FlexTestRow?> GetByIdAsync(int id, CancellationToken ct = default);
    Task InsertAsync(FlexTestRow row, CancellationToken ct = default);  // fejler hvis Id findes
    Task UpdateAsync(FlexTestRow row, CancellationToken ct = default);  // fejler hvis Id ikke findes
    Task UpsertAsync(FlexTestRow row, CancellationToken ct = default); // insert/update
    Task DeleteAsync(int id, CancellationToken ct = default);
}
