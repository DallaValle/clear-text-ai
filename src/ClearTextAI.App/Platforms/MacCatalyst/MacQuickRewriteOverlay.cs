using System.Runtime.InteropServices;
using ClearTextAI.Core.Services.Quick;
using CoreAnimation;
using CoreGraphics;
using Foundation;
using ObjCRuntime;

namespace ClearTextAI.App.Platforms.MacCatalyst;

/// <summary>
/// A small floating indicator shown next to the pointer while a quick rewrite runs.
/// Catalyst windows cannot float over other apps, so this is a native non activating NSPanel,
/// driven through the Objective-C runtime. If it took focus, the paste would land in it.
/// </summary>
public sealed class MacQuickRewriteOverlay : IQuickRewriteOverlay
{
    private const string ObjC = "/usr/lib/libobjc.dylib";

    private const ulong NonactivatingPanelMask = 1 << 7;
    private const ulong BufferedBacking = 2;
    private const long StatusWindowLevel = 25;
    private const ulong JoinAllSpacesAndFullScreen = (1 << 0) | (1 << 8);
    private const long SpinningStyle = 1;
    private const long SmallControlSize = 1;

    private const double Padding = 12;
    private const double Spacing = 10;
    private const double SpinnerSize = 16;
    private const double MaxLabelWidth = 300;
    private const double PointerOffset = 18;

    private static readonly TimeSpan MessageLinger = TimeSpan.FromSeconds(4);

    private IntPtr _panel;
    private IntPtr _spinner;
    private IntPtr _label;
    private CancellationTokenSource? _autoHide;

    public Task ShowBusyAsync(string message) => ShowAsync(message, busy: true, isError: false, autoHide: false);

    public Task ShowMessageAsync(string message, bool isError) =>
        ShowAsync(message, busy: false, isError: isError, autoHide: true);

    public Task HideAsync() => MainThread.InvokeOnMainThreadAsync(() =>
    {
        CancelAutoHide();
        if (_panel != IntPtr.Zero)
        {
            Send(_spinner, "stopAnimation:", IntPtr.Zero);
            Send(_panel, "orderOut:", IntPtr.Zero);
        }
    });

    private Task ShowAsync(string message, bool busy, bool isError, bool autoHide) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            CancelAutoHide();
            EnsurePanel();

            using var text = new NSString(message);
            Send(_label, "setStringValue:", text.Handle);
            Send(_label, "setTextColor:", isError ? Color(0.98, 0.50, 0.45) : Color(1, 1, 1));
            SendBool(_spinner, "setHidden:", !busy);
            Send(_spinner, busy ? "startAnimation:" : "stopAnimation:", IntPtr.Zero);

            Layout(busy);
            Send(_panel, "orderFrontRegardless");

            if (!autoHide)
            {
                return;
            }

            _autoHide = new CancellationTokenSource();
            try
            {
                await Task.Delay(MessageLinger, _autoHide.Token).ConfigureAwait(true);
                Send(_panel, "orderOut:", IntPtr.Zero);
            }
            catch (OperationCanceledException)
            {
            }
        });

    private void CancelAutoHide()
    {
        _autoHide?.Cancel();
        _autoHide?.Dispose();
        _autoHide = null;
    }

    private void EnsurePanel()
    {
        if (_panel != IntPtr.Zero)
        {
            return;
        }

        var frame = new NSRectD(0, 0, 200, 40);
        _panel = InitPanel(Send(Class.GetHandle("NSPanel"), "alloc"), Selector.GetHandle("initWithContentRect:styleMask:backing:defer:"),
            frame, NonactivatingPanelMask, BufferedBacking, false);

        SendLong(_panel, "setLevel:", StatusWindowLevel);
        SendLong(_panel, "setCollectionBehavior:", (long)JoinAllSpacesAndFullScreen);
        SendBool(_panel, "setOpaque:", false);
        SendBool(_panel, "setHasShadow:", true);
        SendBool(_panel, "setIgnoresMouseEvents:", true);
        SendBool(_panel, "setHidesOnDeactivate:", false);
        SendBool(_panel, "setReleasedWhenClosed:", false);
        Send(_panel, "setBackgroundColor:", Send(Class.GetHandle("NSColor"), "clearColor"));

        // Dark appearance keeps the spinner white on the dark card regardless of the system theme.
        using var darkAqua = new NSString("NSAppearanceNameDarkAqua");
        Send(_panel, "setAppearance:", Send(Class.GetHandle("NSAppearance"), "appearanceNamed:", darkAqua.Handle));

        var content = Send(_panel, "contentView");
        SendBool(content, "setWantsLayer:", true);
        var layer = Runtime.GetNSObject<CALayer>(Send(content, "layer"))!;
        layer.BackgroundColor = Rgb(0x1B, 0x24, 0x31);
        layer.BorderColor = Rgb(0x25, 0x63, 0xEB);
        layer.BorderWidth = 1;
        layer.CornerRadius = 10;
        layer.MasksToBounds = true;

        _spinner = InitWithFrame(Send(Class.GetHandle("NSProgressIndicator"), "alloc"), Selector.GetHandle("initWithFrame:"),
            new NSRectD(0, 0, SpinnerSize, SpinnerSize));
        SendLong(_spinner, "setStyle:", SpinningStyle);
        SendLong(_spinner, "setControlSize:", SmallControlSize);
        SendBool(_spinner, "setDisplayedWhenStopped:", false);
        Send(content, "addSubview:", _spinner);

        using var empty = new NSString(string.Empty);
        _label = Send(Class.GetHandle("NSTextField"), "wrappingLabelWithString:", empty.Handle);
        Send(_label, "setFont:", SendDouble(Class.GetHandle("NSFont"), "systemFontOfSize:", 14));
        SendDouble(_label, "setPreferredMaxLayoutWidth:", MaxLabelWidth);
        Send(content, "addSubview:", _label);
    }

    private void Layout(bool busy)
    {
        var labelSize = GetSize(_label, Selector.GetHandle("fittingSize"));
        var leading = busy ? SpinnerSize + Spacing : 0;
        var width = Math.Ceiling(Padding + leading + labelSize.Width + Padding);
        var height = Math.Ceiling(Math.Max(labelSize.Height, SpinnerSize) + (Padding * 2));

        SetFrame(_spinner, Selector.GetHandle("setFrame:"),
            new NSRectD(Padding, (height - SpinnerSize) / 2, SpinnerSize, SpinnerSize));
        SetFrame(_label, Selector.GetHandle("setFrame:"),
            new NSRectD(Padding + leading, (height - labelSize.Height) / 2, labelSize.Width, labelSize.Height));

        // Screen coordinates grow upwards, so the card sits below and to the right of the pointer.
        var pointer = GetPoint(Class.GetHandle("NSEvent"), Selector.GetHandle("mouseLocation"));
        var bounds = VisibleFrameAt(pointer);
        var x = Math.Clamp(pointer.X + PointerOffset, bounds.X, Math.Max(bounds.X, bounds.X + bounds.Width - width));
        var y = Math.Clamp(pointer.Y - PointerOffset - height, bounds.Y, Math.Max(bounds.Y, bounds.Y + bounds.Height - height));

        SetFrameDisplay(_panel, Selector.GetHandle("setFrame:display:"), new NSRectD(x, y, width, height), true);
    }

    private static NSRectD VisibleFrameAt(NSPointD point)
    {
        var screens = Runtime.GetNSObject<NSArray>(Send(Class.GetHandle("NSScreen"), "screens"));
        var visibleFrame = Selector.GetHandle("visibleFrame");

        for (nuint i = 0; screens is not null && i < screens.Count; i++)
        {
            var screen = screens.ValueAt(i);
            var frame = GetRect(screen, Selector.GetHandle("frame"));
            if (point.X >= frame.X && point.X <= frame.X + frame.Width && point.Y >= frame.Y && point.Y <= frame.Y + frame.Height)
            {
                return GetRect(screen, visibleFrame);
            }
        }

        return GetRect(Send(Class.GetHandle("NSScreen"), "mainScreen"), visibleFrame);
    }

    private static CGColor Rgb(byte red, byte green, byte blue) =>
        new(new NFloat(red / 255.0), new NFloat(green / 255.0), new NFloat(blue / 255.0));

    private static IntPtr Color(double red, double green, double blue) =>
        MakeColor(Class.GetHandle("NSColor"), Selector.GetHandle("colorWithSRGBRed:green:blue:alpha:"), red, green, blue, 1);

    private static IntPtr Send(IntPtr target, string selector) => MsgSend(target, Selector.GetHandle(selector));

    private static IntPtr Send(IntPtr target, string selector, IntPtr argument) =>
        MsgSend(target, Selector.GetHandle(selector), argument);

    private static void SendBool(IntPtr target, string selector, bool value) =>
        MsgSendBool(target, Selector.GetHandle(selector), value);

    private static void SendLong(IntPtr target, string selector, long value) =>
        MsgSendLong(target, Selector.GetHandle(selector), value);

    private static IntPtr SendDouble(IntPtr target, string selector, double value) =>
        MsgSendDouble(target, Selector.GetHandle(selector), value);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct NSPointD(double X, double Y);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct NSSizeD(double Width, double Height);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct NSRectD(double X, double Y, double Width, double Height);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr MsgSend(IntPtr target, IntPtr selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr MsgSend(IntPtr target, IntPtr selector, IntPtr argument);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void MsgSendBool(IntPtr target, IntPtr selector, [MarshalAs(UnmanagedType.U1)] bool value);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void MsgSendLong(IntPtr target, IntPtr selector, long value);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr MsgSendDouble(IntPtr target, IntPtr selector, double value);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr MakeColor(IntPtr target, IntPtr selector, double red, double green, double blue, double alpha);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr InitPanel(IntPtr target, IntPtr selector, NSRectD frame, ulong styleMask,
        ulong backing, [MarshalAs(UnmanagedType.U1)] bool defer);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr InitWithFrame(IntPtr target, IntPtr selector, NSRectD frame);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void SetFrame(IntPtr target, IntPtr selector, NSRectD frame);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void SetFrameDisplay(IntPtr target, IntPtr selector, NSRectD frame,
        [MarshalAs(UnmanagedType.U1)] bool display);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern NSSizeD GetSize(IntPtr target, IntPtr selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern NSPointD GetPoint(IntPtr target, IntPtr selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern NSRectD GetRect(IntPtr target, IntPtr selector);
}
