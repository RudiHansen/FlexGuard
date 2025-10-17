// FlexGuard.Core/Abstractions/INewFileManifestEntryStore.cs
using FlexGuard.Core.Models;

namespace FlexGuard.Core.Abstractions;

public interface INewFileManifestEntryStore
{
    Task<IReadOnlyList<NewFileManifestEntry>> GetAllAsync(CancellationToken ct = default);
    Task<NewFileManifestEntry?> GetByIdAsync(string id, CancellationToken ct = default);
    Task InsertAsync(NewFileManifestEntry row, CancellationToken ct = default);
    Task UpdateAsync(NewFileManifestEntry row, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
}