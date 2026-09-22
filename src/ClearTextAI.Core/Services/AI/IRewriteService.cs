using ClearTextAI.Core.Models;

namespace ClearTextAI.Core.Services.AI;

/// <summary>The one call the UI makes: validate, send to the active provider, record history.</summary>
public interface IRewriteService
{
    Task<RewriteResult> RewriteAsync(string text, CancellationToken cancellationToken = default);

    Task<RewriteResult> RewriteAsync(string text, RewriteOptions options, CancellationToken cancellationToken = default);
}
