using System.Runtime.InteropServices;
using ClearTextAI.Core.Services.Settings;
using Microsoft.UI;
using Microsoft.UI.Windowing;

namespace ClearTextAI.App.Platforms.Windows;

/// <summary>
/// Closes the window into the notification area instead of quitting, so the hotkey window stays alive.
/// Ctrl+Alt+I is delivered to that window: destroying it is what makes the shortcut stop.
/// </summary>
public sealed class WindowsBackgroundHost
{
    private const nuint SubclassId = 0xC7A2;
    private const int TrayIconId = 1;
    private const uint TrayCallbackMessage = 0x8001;

    private const uint WmNull = 0x0000;
    private const uint WmDestroy = 0x0002;
    private const uint WmEndSession = 0x0016;
    private const uint WmLButtonUp = 0x0202;
    private const uint WmLButtonDblClk = 0x0203;
    private const uint WmRButtonUp = 0x0205;
    private const uint NinSelect = 0x0400;
    private const uint NinKeySelect = 0x0401;

    private const uint NimAdd = 0x00000000;
    private const uint NimModify = 0x00000001;
    private const uint NimDelete = 0x00000002;
    private const uint NimSetVersion = 0x00000004;
    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;
    private const uint NifInfo = 0x00000010;
    private const uint NiifInfo = 0x00000001;
    private const uint NiifRespectQuietTime = 0x00000080;
    private const uint NotifyIconVersion = 3;

    private const uint MfString = 0x00000000;
    private const uint MfSeparator = 0x00000800;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmNonotify = 0x0080;
    private const uint TpmReturnCmd = 0x0100;
    private const uint CmdOpen = 1;
    private const uint CmdExit = 2;

    private const int SwRestore = 9;
    private const int IdiApplication = 32512;

    private readonly ISettingsService _settings;
    private readonly SubclassProc _subclassProc;

    private Microsoft.UI.Xaml.Window? _platformWindow;
    private AppWindow? _appWindow;
    private IntPtr _hwnd;
    private IntPtr _icon;
    private bool _destroyIcon;
    private bool _iconAdded;
    private bool _announced;
    private bool _started;
    private bool _exitRequested;

    public WindowsBackgroundHost(ISettingsService settings)
    {
        _settings = settings;
        // Rooted for the process lifetime: the OS calls this after the window is hidden.
        _subclassProc = OnWindowMessage;
    }

    public void Start(Microsoft.Maui.Controls.Window window)
    {
        if (_started)
        {
            return;
        }

        _started = true;

        if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window platformWindow)
        {
            Attach(platformWindow);
        }

        _settings.SettingsChanged += OnSettingsChanged;
        ApplyBackgroundSetting();
        WindowsSingleInstance.MarkReady();
    }

    /// <summary>Quits for real. The title bar close only hides the window while background running is on.</summary>
    public void Exit()
    {
        _exitRequested = true;

        var queue = _platformWindow?.DispatcherQueue;
        if (queue is not null)
        {
            queue.TryEnqueue(() => _platformWindow?.Close());
            return;
        }

        Microsoft.Maui.Controls.Application.Current?.Quit();
    }

    private void Attach(Microsoft.UI.Xaml.Window platformWindow)
    {
        _platformWindow = platformWindow;
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(platformWindow);
        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_hwnd));
        _appWindow.Closing += OnClosing;
        SetWindowSubclass(_hwnd, _subclassProc, SubclassId, IntPtr.Zero);
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        var queue = _platformWindow?.DispatcherQueue;
        if (queue is not null)
        {
            queue.TryEnqueue(ApplyBackgroundSetting);
            return;
        }

        ApplyBackgroundSetting();
    }

    private void ApplyBackgroundSetting()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        if (_settings.Current.RunInBackground)
        {
            AddTrayIcon();
            return;
        }

        RemoveTrayIcon();

        // Turning the setting off while hidden would leave no way back to the window.
        if (!IsWindowVisible(_hwnd))
        {
            ShowMainWindow();
        }
    }

    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        // No icon means no way to reopen or quit, so let the window close instead of trapping the process.
        if (!_exitRequested && _settings.Current.RunInBackground && _iconAdded)
        {
            args.Cancel = true;
            _platformWindow?.DispatcherQueue.TryEnqueue(() =>
            {
                sender.Hide();
                NotifyStillRunning();
            });
            return;
        }

        RemoveTrayIcon();
        if (_appWindow is not null)
        {
            _appWindow.Closing -= OnClosing;
        }

        RemoveSubclass();
    }

    private void ShowMainWindow()
    {
        if (_appWindow is null || _platformWindow is null)
        {
            return;
        }

        if (_hwnd != IntPtr.Zero && IsIconic(_hwnd))
        {
            ShowWindow(_hwnd, SwRestore);
        }

        _appWindow.Show();
        _platformWindow.Activate();

        if (_hwnd != IntPtr.Zero)
        {
            SetForegroundWindow(_hwnd);
        }
    }

    private void AddTrayIcon()
    {
        if (_iconAdded || _hwnd == IntPtr.Zero)
        {
            return;
        }

        _icon = LoadTrayIcon();
        var data = CreateIconData();
        data.uFlags = NifMessage | NifIcon | NifTip;
        data.uCallbackMessage = TrayCallbackMessage;
        data.hIcon = _icon;
        data.szTip = "ClearText AI (Ctrl+Alt+I)";

        if (!Shell_NotifyIcon(NimAdd, ref data))
        {
            DestroyOwnedIcon();
            return;
        }

        data.uFlags = 0;
        data.uTimeoutOrVersion = NotifyIconVersion;
        Shell_NotifyIcon(NimSetVersion, ref data);
        _iconAdded = true;
    }

    private void RemoveTrayIcon()
    {
        if (_hwnd != IntPtr.Zero && _iconAdded)
        {
            var data = CreateIconData();
            Shell_NotifyIcon(NimDelete, ref data);
        }

        _iconAdded = false;
        DestroyOwnedIcon();
    }

    private void NotifyStillRunning()
    {
        if (_announced || !_iconAdded)
        {
            return;
        }

        _announced = true;
        var data = CreateIconData();
        data.uFlags = NifInfo;
        data.szInfoTitle = "ClearText AI";
        data.szInfo = "Still running, so Ctrl+Alt+I keeps working. Right-click this icon and choose Exit to quit.";
        data.dwInfoFlags = NiifInfo | NiifRespectQuietTime;
        Shell_NotifyIcon(NimModify, ref data);
    }

    private void HandleTrayClick(uint mouse)
    {
        switch (mouse)
        {
            case WmLButtonUp:
            case WmLButtonDblClk:
            case NinSelect:
            case NinKeySelect:
                ShowMainWindow();
                break;
            case WmRButtonUp:
                ShowTrayMenu();
                break;
        }
    }

    private void ShowTrayMenu()
    {
        var menu = CreatePopupMenu();
        if (menu == IntPtr.Zero)
        {
            return;
        }

        try
        {
            AppendMenu(menu, MfString, CmdOpen, "Open ClearText AI");
            AppendMenu(menu, MfSeparator, 0, null);
            AppendMenu(menu, MfString, CmdExit, "Exit");

            GetCursorPos(out var point);
            SetForegroundWindow(_hwnd);
            var command = TrackPopupMenu(
                menu,
                TpmRightButton | TpmNonotify | TpmReturnCmd,
                point.X,
                point.Y,
                0,
                _hwnd,
                IntPtr.Zero);
            PostMessage(_hwnd, WmNull, IntPtr.Zero, IntPtr.Zero);

            if (command == CmdOpen)
            {
                ShowMainWindow();
            }
            else if (command == CmdExit)
            {
                Exit();
            }
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    private NotifyIconData CreateIconData() => new()
    {
        cbSize = Marshal.SizeOf<NotifyIconData>(),
        hWnd = _hwnd,
        uID = TrayIconId,
        szTip = string.Empty,
        szInfo = string.Empty,
        szInfoTitle = string.Empty,
    };

    private IntPtr LoadTrayIcon()
    {
        var path = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(path))
        {
            _ = ExtractIconEx(path, 0, out var large, out var small, 1);
            if (large != IntPtr.Zero && large != small)
            {
                DestroyIcon(large);
            }

            var extracted = small != IntPtr.Zero ? small : large;
            if (extracted != IntPtr.Zero)
            {
                _destroyIcon = true;
                return extracted;
            }
        }

        _destroyIcon = false;
        return LoadSharedIcon(IntPtr.Zero, (IntPtr)IdiApplication);
    }

    private void DestroyOwnedIcon()
    {
        if (_destroyIcon && _icon != IntPtr.Zero)
        {
            DestroyIcon(_icon);
        }

        _icon = IntPtr.Zero;
        _destroyIcon = false;
    }

    private void RemoveSubclass()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        RemoveWindowSubclass(_hwnd, _subclassProc, SubclassId);
        _hwnd = IntPtr.Zero;
    }

    private IntPtr OnWindowMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, nuint id, IntPtr data)
    {
        if (WindowsSingleInstance.ShowMessage != 0 && msg == WindowsSingleInstance.ShowMessage)
        {
            ShowMainWindow();
            return IntPtr.Zero;
        }

        if (msg == TrayCallbackMessage)
        {
            HandleTrayClick((uint)lParam);
            return IntPtr.Zero;
        }

        if (msg == WmDestroy || msg == WmEndSession)
        {
            RemoveTrayIcon();
        }

        return DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    private delegate IntPtr SubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, nuint id, IntPtr data);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Shell_NotifyIcon(uint message, ref NotifyIconData data);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string file, int index, out IntPtr large, out IntPtr small, uint count);

    [DllImport("user32.dll", EntryPoint = "LoadIconW", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadSharedIcon(IntPtr hInstance, IntPtr iconName);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int command);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr menu, uint flags, nuint idNewItem, string? item);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr hwnd, IntPtr rect);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc callback, nuint id, IntPtr data);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool RemoveWindowSubclass(IntPtr hWnd, SubclassProc callback, nuint id);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
