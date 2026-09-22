using ClearTextAI.Core.Services.SecureStorage;

namespace ClearTextAI.App.Platforms.MacCatalyst;

/// <summary>macOS Keychain, through MAUI's platform secure storage.</summary>
public sealed class KeychainCredentialStore : ISecureCredentialStore
{
    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
        Microsoft.Maui.Storage.SecureStorage.Default.GetAsync(key);

    public Task SetAsync(string key, string value, CancellationToken cancellationToken = default) =>
        Microsoft.Maui.Storage.SecureStorage.Default.SetAsync(key, value);

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        Microsoft.Maui.Storage.SecureStorage.Default.Remove(key);
        return Task.CompletedTask;
    }
}
