using System.Runtime.InteropServices;
using ClearTextAI.Core.Services.Quick;

namespace ClearTextAI.App.Platforms.Windows;

/// <summary>
/// Ctrl+Alt+I registered with the OS, so it fires while another application has focus.
/// WM_HOTKEY is delivered to the main window, which is subclassed to intercept it.
/// </summary>
public sealed class WindowsGlobalHotkeyService : IGlobalHotkeyService, IDisposable
{
    private const int HotkeyId = 0xC7A1;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkI = 0x49;
    private const uint WmHotkey = 0x0312;

    private readonly SubclassProc _subclassProc;

    private IntPtr _hwnd;

    public WindowsGlobalHotkeyService()
    {
        // Held in a field so the GC cannot collect the callback the OS still points at.
        _subclassProc = OnWindowMessage;
    }

    public bool IsSupported => true;

    public bool IsRegistered { get; private set; }

    public string DisplayShortcut => QuickRewrite.ShortcutDisplay;

    public event EventHandler? Pressed;

    /// <summary>Called once the MAUI window exists, since the hotkey needs a window to deliver to.</summary>
    public void Attach(IntPtr windowHandle)
    {
        if (_hwnd != IntPtr.Zero)
        {
            return;
        }

        _hwnd = windowHandle;
        SetWindowSubclass(_hwnd, _subclassProc, HotkeyId, IntPtr.Zero);
    }

    public bool Register()
    {
        if (_hwnd == IntPtr.Zero || IsRegistered)
        {
            return IsRegistered;
        }

        // MOD_NOREPEAT stops a held key from queueing a burst of rewrites.
        IsRegistered = RegisterHotKey(_hwnd, HotkeyId, ModControl | ModAlt | ModNoRepeat, VkI);
        return IsRegistered;
    }

    public void Unregister()
    {
        if (!IsRegistered)
        {
            return;
        }

        UnregisterHotKey(_hwnd, HotkeyId);
        IsRegistered = false;
    }

    public void Dispose()
    {
        Unregister();

        if (_hwnd != IntPtr.Zero)
        {
            RemoveWindowSubclass(_hwnd, _subclassProc, HotkeyId);
            _hwnd = IntPtr.Zero;
        }
    }

    private IntPtr OnWindowMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, nuint id, IntPtr data)
    {
        if (msg == WmHotkey && wParam == HotkeyId)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
            return IntPtr.Zero;
        }

        return DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    private delegate IntPtr SubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, nuint id, IntPtr data);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc callback, nuint id, IntPtr data);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool RemoveWindowSubclass(IntPtr hWnd, SubclassProc callback, nuint id);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
