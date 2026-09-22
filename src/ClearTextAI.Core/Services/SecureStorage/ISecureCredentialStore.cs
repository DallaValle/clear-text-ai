namespace ClearTextAI.Core.Services.SecureStorage;

/// <summary>
/// Platform-backed secret storage: Windows Credential Manager, macOS Keychain.
/// Implementations live in the app project because they are platform specific.
/// </summary>
public interface ISecureCredentialStore
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task SetAsync(string key, string value, CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
