using System.Diagnostics;
using System.Runtime.InteropServices;
using ClearTextAI.Core.Services.Quick;
using Foundation;
using UIKit;

namespace ClearTextAI.App.Platforms.MacCatalyst;

/// <summary>
/// Reads and replaces the selection in the frontmost app by sending it Cmd+C and Cmd+V, the one
/// technique every Mac app honours. Posting keystrokes needs the Accessibility permission, and
/// whatever was on the pasteboard is put back afterwards.
/// </summary>
public sealed class MacSelectionTextService : ISelectionTextService
{
    private const string ApplicationServices = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    private const ushort KeyCodeC = 8;
    private const ushort KeyCodeV = 9;
    private const ulong CommandFlag = 0x100000;
    private const uint HidEventTap = 0;

    private static readonly TimeSpan CaptureTimeout = TimeSpan.FromMilliseconds(900);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(40);

    /// <summary>Apps read the pasteboard when they handle the paste, so it must not be restored before that.</summary>
    private static readonly TimeSpan PasteSettle = TimeSpan.FromMilliseconds(250);

    private NSDictionary[]? _savedItems;

    public bool IsSupported => true;

    public async Task<string?> CaptureSelectionAsync(CancellationToken cancellationToken = default)
    {
        EnsureAccessibilityAccess();

        var changeCount = await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _savedItems = UIPasteboard.General.Items;
            return UIPasteboard.General.ChangeCount;
        }).ConfigureAwait(false);

        PostCommandKey(KeyCodeC);

        // The change count, not the content, tells a fresh copy apart from what was already there.
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < CaptureTimeout)
        {
            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);

            var captured = await MainThread.InvokeOnMainThreadAsync(() =>
                UIPasteboard.General.ChangeCount != changeCount ? UIPasteboard.General.String ?? string.Empty : null)
                .ConfigureAwait(false);

            if (captured is not null)
            {
                if (captured.Length == 0)
                {
                    break;
                }

                return captured;
            }
        }

        await RestorePasteboardAsync().ConfigureAwait(false);
        return null;
    }

    public async Task ReplaceSelectionAsync(string text, CancellationToken cancellationToken = default)
    {
        await MainThread.InvokeOnMainThreadAsync(() => UIPasteboard.General.String = text).ConfigureAwait(false);

        PostCommandKey(KeyCodeV);

        await Task.Delay(PasteSettle, cancellationToken).ConfigureAwait(false);
        await RestorePasteboardAsync().ConfigureAwait(false);
    }

    private Task RestorePasteboardAsync()
    {
        var saved = _savedItems;
        _savedItems = null;

        return saved is null
            ? Task.CompletedTask
            : MainThread.InvokeOnMainThreadAsync(() => UIPasteboard.General.Items = saved);
    }

    /// <summary>
    /// Asks once through the system prompt, then explains where the switch lives. The grant is tied
    /// to the code signature, so an ad hoc signed rebuild shows as allowed while it is not.
    /// </summary>
    private static void EnsureAccessibilityAccess()
    {
        using var key = new NSString("AXTrustedCheckOptionPrompt");
        using var options = NSDictionary.FromObjectAndKey(NSNumber.FromBoolean(true), key);

        if (!AXIsProcessTrustedWithOptions(options.Handle))
        {
            throw new QuickRewriteUnavailableException(
                "Allow ClearText AI in System Settings, Privacy & Security, Accessibility, then press the shortcut again. " +
                "If it is already on, remove it from the list with the minus button and press the shortcut again.");
        }
    }

    /// <summary>
    /// The flags are set explicitly, so the Shift key still held from the shortcut
    /// does not turn Cmd+C into Cmd+Shift+C.
    /// </summary>
    private static void PostCommandKey(ushort keyCode)
    {
        foreach (var down in new[] { true, false })
        {
            var keyEvent = CGEventCreateKeyboardEvent(IntPtr.Zero, keyCode, down);
            if (keyEvent == IntPtr.Zero)
            {
                continue;
            }

            CGEventSetFlags(keyEvent, CommandFlag);
            CGEventPost(HidEventTap, keyEvent);
            CFRelease(keyEvent);
        }
    }

    [DllImport(ApplicationServices)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool AXIsProcessTrustedWithOptions(IntPtr options);

    [DllImport(ApplicationServices)]
    private static extern IntPtr CGEventCreateKeyboardEvent(IntPtr source, ushort keyCode,
        [MarshalAs(UnmanagedType.U1)] bool keyDown);

    [DllImport(ApplicationServices)]
    private static extern void CGEventSetFlags(IntPtr keyEvent, ulong flags);

    [DllImport(ApplicationServices)]
    private static extern void CGEventPost(uint tap, IntPtr keyEvent);

    [DllImport(CoreFoundation)]
    private static extern void CFRelease(IntPtr handle);
}
