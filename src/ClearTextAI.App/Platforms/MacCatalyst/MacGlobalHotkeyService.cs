using System.Runtime.InteropServices;
using ClearTextAI.Core.Services.Quick;

namespace ClearTextAI.App.Platforms.MacCatalyst;

/// <summary>
/// Cmd+Shift+I registered through Carbon, the only public macOS API for a system wide
/// shortcut that needs no Accessibility permission. Carbon delivers the press on the main thread.
/// </summary>
public sealed class MacGlobalHotkeyService : IGlobalHotkeyService, IDisposable
{
    private const string Carbon = "/System/Library/Frameworks/Carbon.framework/Carbon";

    private const uint KeyCodeI = 34;
    private const uint CmdKey = 0x0100;
    private const uint ShiftKey = 0x0200;
    private const uint KeyboardEventClass = 0x6B657962; // 'keyb'
    private const uint HotKeyPressedKind = 5;
    private const uint Signature = 0x43544149; // 'CTAI'

    private static MacGlobalHotkeyService? _instance;

    // Held in a static field so the GC cannot collect the callback Carbon still points at.
    private static readonly EventHandlerProc Handler = OnHotKeyEvent;

    private IntPtr _handlerRef;
    private IntPtr _hotKeyRef;

    public bool IsSupported => true;

    public bool IsRegistered => _hotKeyRef != IntPtr.Zero;

    public string DisplayShortcut => QuickRewrite.ShortcutDisplay;

    public event EventHandler? Pressed;

    public bool Register()
    {
        if (IsRegistered)
        {
            return true;
        }

        _instance = this;

        if (_handlerRef == IntPtr.Zero)
        {
            var types = new[] { new EventTypeSpec { EventClass = KeyboardEventClass, EventKind = HotKeyPressedKind } };
            if (InstallEventHandler(GetApplicationEventTarget(), Marshal.GetFunctionPointerForDelegate(Handler),
                    (uint)types.Length, types, IntPtr.Zero, out _handlerRef) != 0)
            {
                _handlerRef = IntPtr.Zero;
                return false;
            }
        }

        // Fails with eventHotKeyExistsErr when another app already owns the combination.
        var id = new EventHotKeyId { Signature = Signature, Id = 1 };
        if (RegisterEventHotKey(KeyCodeI, CmdKey | ShiftKey, id, GetApplicationEventTarget(), 0, out _hotKeyRef) != 0)
        {
            _hotKeyRef = IntPtr.Zero;
        }

        return IsRegistered;
    }

    public void Unregister()
    {
        if (!IsRegistered)
        {
            return;
        }

        UnregisterEventHotKey(_hotKeyRef);
        _hotKeyRef = IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();

        if (_handlerRef != IntPtr.Zero)
        {
            RemoveEventHandler(_handlerRef);
            _handlerRef = IntPtr.Zero;
        }
    }

    [ObjCRuntime.MonoPInvokeCallback(typeof(EventHandlerProc))]
    private static int OnHotKeyEvent(IntPtr callRef, IntPtr eventRef, IntPtr userData)
    {
        _instance?.Pressed?.Invoke(_instance, EventArgs.Empty);
        return 0;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int EventHandlerProc(IntPtr callRef, IntPtr eventRef, IntPtr userData);

    [StructLayout(LayoutKind.Sequential)]
    private struct EventTypeSpec
    {
        public uint EventClass;
        public uint EventKind;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct EventHotKeyId
    {
        public uint Signature;
        public uint Id;
    }

    [DllImport(Carbon)]
    private static extern IntPtr GetApplicationEventTarget();

    [DllImport(Carbon)]
    private static extern int InstallEventHandler(IntPtr target, IntPtr handler, uint typeCount,
        EventTypeSpec[] types, IntPtr userData, out IntPtr handlerRef);

    [DllImport(Carbon)]
    private static extern int RemoveEventHandler(IntPtr handlerRef);

    [DllImport(Carbon)]
    private static extern int RegisterEventHotKey(uint keyCode, uint modifiers, EventHotKeyId id,
        IntPtr target, uint options, out IntPtr hotKeyRef);

    [DllImport(Carbon)]
    private static extern int UnregisterEventHotKey(IntPtr hotKeyRef);
}
