using ClearTextAI.Core.Models;

namespace ClearTextAI.Core.Services.History;

/// <summary>Local-only rewrite history.</summary>
public interface IHistoryStore
{
    /// <summary>Newest first.</summary>
    Task<IReadOnlyList<RewriteHistoryItem>> GetAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(RewriteHistoryItem item, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
