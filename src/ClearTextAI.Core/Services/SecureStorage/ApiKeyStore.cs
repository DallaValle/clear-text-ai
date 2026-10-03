using ClearTextAI.Core.Models;
using ClearTextAI.Core.Services.AI;

namespace ClearTextAI.Core.Services.SecureStorage;

public sealed class ApiKeyStore : IApiKeyStore
{
    private const string KeyPrefix = "cleartextai.apikey.";

    private readonly ISecureCredentialStore _credentialStore;

    public ApiKeyStore(ISecureCredentialStore credentialStore)
    {
        _credentialStore = credentialStore;
    }

    public async Task<string?> GetAsync(AiProviderKind provider, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = await _credentialStore.GetAsync(KeyFor(provider), cancellationToken).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never surface the platform exception: it can carry the key or the store's internals.
            throw AiProviderException.For(AiErrorKind.CredentialStoreUnavailable, provider.ToString(), ex);
        }
    }

    public async Task<bool> HasKeyAsync(AiProviderKind provider, CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync(provider, cancellationToken).ConfigureAwait(false) is not null;
        }
        catch (AiProviderException)
        {
            return false;
        }
    }

    public async Task SetAsync(AiProviderKind provider, string apiKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        await GuardAsync(provider, () => _credentialStore.SetAsync(KeyFor(provider), apiKey.Trim(), cancellationToken))
            .ConfigureAwait(false);
    }

    public Task RemoveAsync(AiProviderKind provider, CancellationToken cancellationToken = default) =>
        GuardAsync(provider, () => _credentialStore.RemoveAsync(KeyFor(provider), cancellationToken));

    // A store failure must read as a credential problem, not as a generic save error.
    private static async Task GuardAsync(AiProviderKind provider, Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw AiProviderException.For(AiErrorKind.CredentialStoreUnavailable, provider.ToString(), ex);
        }
    }

    private static string KeyFor(AiProviderKind provider) =>
        KeyPrefix + provider.ToString().ToLowerInvariant();
}
