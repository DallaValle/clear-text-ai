using System.Runtime.InteropServices;
using System.Text;
using ClearTextAI.Core.Services.SecureStorage;

namespace ClearTextAI.App.Platforms.Windows;

/// <summary>
/// Windows Credential Manager, through the advapi32 credential API. Secrets are stored
/// encrypted under the signed-in user account and never touch the app's own files.
/// Works for both packaged and unpackaged builds.
/// </summary>
public sealed class WindowsCredentialStore : ISecureCredentialStore
{
    private const string TargetPrefix = "ClearTextAI:";
    private const uint CredTypeGeneric = 1;
    private const uint CredPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var handle = IntPtr.Zero;

        try
        {
            if (!CredReadW(TargetFor(key), CredTypeGeneric, 0, out handle))
            {
                var error = Marshal.GetLastWin32Error();
                if (error == ErrorNotFound)
                {
                    return Task.FromResult<string?>(null);
                }

                throw new InvalidOperationException($"Credential Manager returned error {error}.");
            }

            var credential = Marshal.PtrToStructure<Credential>(handle);
            if (credential.CredentialBlobSize == 0 || credential.CredentialBlob == IntPtr.Zero)
            {
                return Task.FromResult<string?>(null);
            }

            var value = Marshal.PtrToStringUni(credential.CredentialBlob, (int)credential.CredentialBlobSize / 2);
            return Task.FromResult<string?>(value);
        }
        finally
        {
            if (handle != IntPtr.Zero)
            {
                CredFree(handle);
            }
        }
    }

    public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var blob = Marshal.StringToCoTaskMemUni(value);
        var blobSize = Encoding.Unicode.GetByteCount(value);

        try
        {
            var credential = new Credential
            {
                Type = CredTypeGeneric,
                TargetName = TargetFor(key),
                CredentialBlob = blob,
                CredentialBlobSize = (uint)blobSize,
                Persist = CredPersistLocalMachine,
                UserName = Environment.UserName,
            };

            if (!CredWriteW(ref credential, 0))
            {
                throw new InvalidOperationException($"Credential Manager returned error {Marshal.GetLastWin32Error()}.");
            }
        }
        finally
        {
            // Overwrite the copy of the secret before handing the memory back.
            for (var i = 0; i < blobSize; i++)
            {
                Marshal.WriteByte(blob, i, 0);
            }

            Marshal.FreeCoTaskMem(blob);
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!CredDeleteW(TargetFor(key), CredTypeGeneric, 0))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != ErrorNotFound)
            {
                throw new InvalidOperationException($"Credential Manager returned error {error}.");
            }
        }

        return Task.CompletedTask;
    }

    private static string TargetFor(string key) => TargetPrefix + key;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags;
        public uint Type;
        [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] public string? UserName;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredReadW(string target, uint type, uint reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWriteW([In] ref Credential userCredential, uint flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDeleteW(string target, uint type, uint flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}
