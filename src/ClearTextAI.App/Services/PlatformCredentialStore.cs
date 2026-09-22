using ClearTextAI.Core.Services.SecureStorage;

namespace ClearTextAI.App.Services;

/// <summary>
/// Resolves to the OS credential store of the running platform: Windows Credential Manager
/// or the macOS Keychain. Kept as a thin forwarder so DI registration stays platform neutral.
/// </summary>
public sealed class PlatformCredentialStore : ISecureCredentialStore
{
    private readonly ISecureCredentialStore _inner;

    public PlatformCredentialStore()
    {
#if WINDOWS
        _inner = new Platforms.Windows.WindowsCredentialStore();
#elif MACCATALYST
        _inner = new Platforms.MacCatalyst.KeychainCredentialStore();
#else
        throw new PlatformNotSupportedException("ClearText AI supports Windows and macOS.");
#endif
    }

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
        _inner.GetAsync(key, cancellationToken);

    public Task SetAsync(string key, string value, CancellationToken cancellationToken = default) =>
        _inner.SetAsync(key, value, cancellationToken);

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        _inner.RemoveAsync(key, cancellationToken);
}
