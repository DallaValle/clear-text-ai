using System.Runtime.InteropServices;
using System.Text;
using ClearTextAI.Core.Services.SecureStorage;

namespace ClearTextAI.App.Platforms.MacCatalyst;

/// <summary>
/// macOS login Keychain through the classic SecKeychain API. On Mac Catalyst both MAUI SecureStorage
/// and SecItem go to the data protection keychain, which rejects apps without a team signed
/// application identifier (MissingEntitlement), so local and ad hoc builds could never store a key.
/// </summary>
public sealed class KeychainCredentialStore : ISecureCredentialStore
{
    private const string Service = "com.cleartextai.app";
    private const string SecurityLibrary = "/System/Library/Frameworks/Security.framework/Security";
    private const int ErrSecSuccess = 0;
    private const int ErrSecItemNotFound = -25300;

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var status = Find(key, out var item, out var length, out var data);
        if (status == ErrSecItemNotFound)
        {
            return Task.FromResult<string?>(null);
        }

        EnsureSuccess("read", status);
        try
        {
            return Task.FromResult<string?>(Marshal.PtrToStringUTF8(data, (int)length));
        }
        finally
        {
            SecKeychainItemFreeContent(IntPtr.Zero, data);
            CFRelease(item);
        }
    }

    public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var status = Find(key, out var item, out _, out _, withData: false);

        if (status == ErrSecItemNotFound)
        {
            var service = Encoding.UTF8.GetBytes(Service);
            var account = Encoding.UTF8.GetBytes(key);
            status = SecKeychainAddGenericPassword(IntPtr.Zero,
                (uint)service.Length, service, (uint)account.Length, account,
                (uint)bytes.Length, bytes, IntPtr.Zero);
        }
        else if (status == ErrSecSuccess)
        {
            try
            {
                status = SecKeychainItemModifyAttributesAndData(item, IntPtr.Zero, (uint)bytes.Length, bytes);
            }
            finally
            {
                CFRelease(item);
            }
        }

        EnsureSuccess("write", status);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var status = Find(key, out var item, out _, out _, withData: false);
        if (status == ErrSecItemNotFound)
        {
            return Task.CompletedTask;
        }

        EnsureSuccess("remove", status);
        try
        {
            EnsureSuccess("remove", SecKeychainItemDelete(item));
        }
        finally
        {
            CFRelease(item);
        }

        return Task.CompletedTask;
    }

    private static int Find(string key, out IntPtr item, out uint length, out IntPtr data, bool withData = true)
    {
        var service = Encoding.UTF8.GetBytes(Service);
        var account = Encoding.UTF8.GetBytes(key);
        length = 0;
        data = IntPtr.Zero;

        return withData
            ? SecKeychainFindGenericPassword(IntPtr.Zero, (uint)service.Length, service,
                (uint)account.Length, account, out length, out data, out item)
            : SecKeychainFindGenericPassword(IntPtr.Zero, (uint)service.Length, service,
                (uint)account.Length, account, IntPtr.Zero, IntPtr.Zero, out item);
    }

    // The message carries only the OSStatus, never the key.
    private static void EnsureSuccess(string action, int status)
    {
        if (status != ErrSecSuccess)
        {
            throw new InvalidOperationException($"Keychain {action} failed with OSStatus {status}.");
        }
    }

    [DllImport(SecurityLibrary)]
    private static extern int SecKeychainFindGenericPassword(IntPtr keychain,
        uint serviceLength, byte[] service, uint accountLength, byte[] account,
        out uint passwordLength, out IntPtr passwordData, out IntPtr item);

    [DllImport(SecurityLibrary)]
    private static extern int SecKeychainFindGenericPassword(IntPtr keychain,
        uint serviceLength, byte[] service, uint accountLength, byte[] account,
        IntPtr passwordLength, IntPtr passwordData, out IntPtr item);

    [DllImport(SecurityLibrary)]
    private static extern int SecKeychainAddGenericPassword(IntPtr keychain,
        uint serviceLength, byte[] service, uint accountLength, byte[] account,
        uint passwordLength, byte[] password, IntPtr item);

    [DllImport(SecurityLibrary)]
    private static extern int SecKeychainItemModifyAttributesAndData(IntPtr item,
        IntPtr attributes, uint length, byte[] data);

    [DllImport(SecurityLibrary)]
    private static extern int SecKeychainItemDelete(IntPtr item);

    [DllImport(SecurityLibrary)]
    private static extern int SecKeychainItemFreeContent(IntPtr attributes, IntPtr data);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr handle);
}
