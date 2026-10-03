using System.Runtime.InteropServices;
using Foundation;
using ObjCRuntime;

namespace ClearTextAI.App.Platforms.MacCatalyst;

/// <summary>
/// Keeps the app reachable from the menu bar once its window is closed, so the global shortcut
/// stays available. Without a window the Dock icon is hidden, like any menu bar utility.
/// Catalyst has no binding for NSStatusItem, so it is driven through the Objective-C runtime.
/// </summary>
public sealed class MacMenuBarItem : NSObject
{
    private const string ObjC = "/usr/lib/libobjc.dylib";
    private const double VariableLength = -1;
    private const long RegularPolicy = 0;
    private const long AccessoryPolicy = 1;

    private readonly Action _open;
    private IntPtr _statusItem;

    public MacMenuBarItem(Action open)
    {
        _open = open;
    }

    public void Install()
    {
        if (_statusItem != IntPtr.Zero)
        {
            return;
        }

        var statusBar = Send(Class.GetHandle("NSStatusBar"), "systemStatusBar");
        _statusItem = Send(SendDouble(statusBar, "statusItemWithLength:", VariableLength), "retain");

        using var symbol = new NSString("text.badge.checkmark");
        using var title = new NSString("ClearText AI");
        var image = Send(Class.GetHandle("NSImage"), "imageWithSystemSymbolName:accessibilityDescription:", symbol.Handle, title.Handle);
        SendBool(image, "setTemplate:", true);
        Send(Send(_statusItem, "button"), "setImage:", image);

        var menu = Send(Send(Class.GetHandle("NSMenu"), "alloc"), "initWithTitle:", title.Handle);
        AddItem(menu, "Open ClearText AI", "openWindow:", Handle, "o");
        Send(menu, "addItem:", Send(Class.GetHandle("NSMenuItem"), "separatorItem"));
        AddItem(menu, "Quit ClearText AI", "terminate:", Application, "q");
        Send(_statusItem, "setMenu:", menu);
    }

    /// <summary>Back to a regular app with a Dock icon, in front of whatever the user was using.</summary>
    public static void EnterForeground()
    {
        SendLong(Application, "setActivationPolicy:", RegularPolicy);
        SendBool(Application, "activateIgnoringOtherApps:", true);
    }

    public static void EnterBackground() => SendLong(Application, "setActivationPolicy:", AccessoryPolicy);

    [Export("openWindow:")]
    public void OpenWindow(NSObject sender) => _open();

    private static IntPtr Application => Send(Class.GetHandle("NSApplication"), "sharedApplication");

    private static void AddItem(IntPtr menu, string title, string action, IntPtr target, string key)
    {
        using var text = new NSString(title);
        using var keyEquivalent = new NSString(key);
        var item = MsgSend(Send(Class.GetHandle("NSMenuItem"), "alloc"),
            Selector.GetHandle("initWithTitle:action:keyEquivalent:"), text.Handle, Selector.GetHandle(action), keyEquivalent.Handle);
        Send(item, "setTarget:", target);
        Send(menu, "addItem:", item);
    }

    private static IntPtr Send(IntPtr target, string selector) => MsgSend(target, Selector.GetHandle(selector));

    private static IntPtr Send(IntPtr target, string selector, IntPtr argument) =>
        MsgSend(target, Selector.GetHandle(selector), argument);

    private static IntPtr Send(IntPtr target, string selector, IntPtr first, IntPtr second) =>
        MsgSend(target, Selector.GetHandle(selector), first, second);

    private static void SendBool(IntPtr target, string selector, bool value) =>
        MsgSendBool(target, Selector.GetHandle(selector), value);

    private static void SendLong(IntPtr target, string selector, long value) =>
        MsgSendLong(target, Selector.GetHandle(selector), value);

    private static IntPtr SendDouble(IntPtr target, string selector, double value) =>
        MsgSendDouble(target, Selector.GetHandle(selector), value);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr MsgSend(IntPtr target, IntPtr selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr MsgSend(IntPtr target, IntPtr selector, IntPtr argument);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr MsgSend(IntPtr target, IntPtr selector, IntPtr first, IntPtr second);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr MsgSend(IntPtr target, IntPtr selector, IntPtr first, IntPtr second, IntPtr third);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void MsgSendBool(IntPtr target, IntPtr selector, [MarshalAs(UnmanagedType.U1)] bool value);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void MsgSendLong(IntPtr target, IntPtr selector, long value);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr MsgSendDouble(IntPtr target, IntPtr selector, double value);
}
