using FlexGuard.Core.Models;

namespace FlexGuard.Core.Abstractions;

public interface INewFileManifestStore
{
    Task<IReadOnlyList<NewFileManifest>> GetAllAsync(CancellationToken ct = default);
    Task<NewFileManifest?> GetByIdAsync(string id, CancellationToken ct = default);
    Task InsertAsync(NewFileManifest row, CancellationToken ct = default);
    Task UpdateAsync(NewFileManifest row, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
}