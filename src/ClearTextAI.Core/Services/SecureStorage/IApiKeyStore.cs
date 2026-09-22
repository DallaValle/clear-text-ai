using ClearTextAI.Core.Models;

namespace ClearTextAI.Core.Services.SecureStorage;

/// <summary>Provider-aware wrapper over <see cref="ISecureCredentialStore"/>.</summary>
public interface IApiKeyStore
{
    Task<string?> GetAsync(AiProviderKind provider, CancellationToken cancellationToken = default);

    Task<bool> HasKeyAsync(AiProviderKind provider, CancellationToken cancellationToken = default);

    Task SetAsync(AiProviderKind provider, string apiKey, CancellationToken cancellationToken = default);

    Task RemoveAsync(AiProviderKind provider, CancellationToken cancellationToken = default);
}
