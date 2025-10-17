using FlexGuard.Core.Models;

namespace FlexGuard.Core.Abstractions;

public interface IFlexTestTableStore
{
    Task<IReadOnlyList<FlexTestRow>> GetAllAsync(CancellationToken ct = default);
    Task<FlexTestRow?> GetByIdAsync(string id, CancellationToken ct = default);
    Task InsertAsync(FlexTestRow row, CancellationToken ct = default);
    Task UpdateAsync(FlexTestRow row, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
}
