using ClearTextAI.Core.Models;

namespace ClearTextAI.Core.Services.AI;

/// <summary>
/// The abstraction every AI backend implements. Adding a provider means adding one class here
/// and registering it; nothing in the UI or the view models changes.
/// </summary>
public interface IAiProvider
{
    AiProviderKind Kind { get; }

    string DisplayName { get; }

    /// <summary>Rewrites <paramref name="text"/> and returns only the rewritten text.</summary>
    /// <exception cref="AiProviderException">Any failure, already translated into a message the user can act on.</exception>
    Task<string> RewriteTextAsync(
        string text,
        RewriteOptions options,
        CancellationToken cancellationToken = default);
}
