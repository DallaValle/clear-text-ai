using System.Runtime.InteropServices;
using ClearTextAI.Core.Services.Quick;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Colors = Microsoft.Maui.Graphics.Colors;
using RectInt32 = global::Windows.Graphics.RectInt32;

namespace ClearTextAI.App.Platforms.Windows;

/// <summary>
/// A small floating indicator shown near the caret while a quick rewrite runs.
/// It carries WS_EX_NOACTIVATE: if it took focus, the synthetic paste would land in the
/// overlay instead of the application the user is actually typing in.
/// </summary>
public sealed class WindowsQuickRewriteOverlay : IQuickRewriteOverlay
{
    private const int GwlExStyle = -20;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExTopmost = 0x00000008;

    // Logical units, the same coordinate space MAUI lays out in.
    private const int OverlayWidth = 280;
    private const int OverlayHeight = 66;
    private const int CursorOffset = 18;

    private static readonly TimeSpan MessageLinger = TimeSpan.FromSeconds(4);

    private Window? _window;
    private AppWindow? _appWindow;
    private IntPtr _hwnd;
    private ActivityIndicator? _spinner;
    private Label? _label;
    private CancellationTokenSource? _autoHide;

    public Task ShowBusyAsync(string message) => ShowAsync(message, busy: true, isError: false, autoHide: false);

    public Task ShowMessageAsync(string message, bool isError) =>
        ShowAsync(message, busy: false, isError: isError, autoHide: true);

    public Task HideAsync() => MainThread.InvokeOnMainThreadAsync(() =>
    {
        CancelAutoHide();
        _appWindow?.Hide();
    });

    private Task ShowAsync(string message, bool busy, bool isError, bool autoHide) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            CancelAutoHide();
            EnsureWindow();

            if (_label is null || _spinner is null || _appWindow is null)
            {
                return;
            }

            _label.Text = message;
            _label.TextColor = isError ? Colors.Salmon : Colors.White;
            _spinner.IsVisible = busy;
            _spinner.IsRunning = busy;

            MoveNearCursor();
            _appWindow.Show(activateWindow: false);

            if (!autoHide)
            {
                return;
            }

            _autoHide = new CancellationTokenSource();
            var token = _autoHide.Token;

            try
            {
                await Task.Delay(MessageLinger, token).ConfigureAwait(true);
                _appWindow.Hide();
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

    private void EnsureWindow()
    {
        if (_window is not null)
        {
            return;
        }

        _spinner = new ActivityIndicator { Color = Colors.White, WidthRequest = 18, HeightRequest = 18 };
        _label = new Label { TextColor = Colors.White, FontSize = 14, VerticalOptions = LayoutOptions.Center };

        var page = new ContentPage
        {
            BackgroundColor = Colors.Transparent,
            Content = new Border
            {
                BackgroundColor = Color.FromArgb("#1B2431"),
                Stroke = Color.FromArgb("#2563EB"),
                StrokeThickness = 1,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                Padding = 12,
                Margin = 4,
                Content = new HorizontalStackLayout
                {
                    Spacing = 10,
                    VerticalOptions = LayoutOptions.Center,
                    Children = { _spinner, _label },
                },
            },
        };

        _window = new Window(page) { Title = "ClearText AI" };
        Application.Current!.OpenWindow(_window);

        var platformWindow = _window.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
        if (platformWindow is null)
        {
            return;
        }

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(platformWindow);
        _hwnd = hwnd;
        var style = GetWindowLong(hwnd, GwlExStyle);
        SetWindowLong(hwnd, GwlExStyle, (IntPtr)((long)style | WsExNoActivate | WsExToolWindow | WsExTopmost));

        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hwnd));

        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        _appWindow.Hide();
    }

    private void MoveNearCursor()
    {
        if (_appWindow is null || _hwnd == IntPtr.Zero)
        {
            return;
        }

        var x = 200;
        var y = 200;

        if (GetCursorPos(out var point))
        {
            x = point.X + CursorOffset;
            y = point.Y + CursorOffset;
        }

        // MoveAndResize works in physical pixels while MAUI lays out in logical ones. Without
        // this scaling the padding eats the whole box on a scaled display and the content vanishes.
        var scale = GetDpiForWindow(_hwnd) / 96.0;
        var width = (int)Math.Round(OverlayWidth * scale);
        var height = (int)Math.Round(OverlayHeight * scale);

        _appWindow.MoveAndResize(new RectInt32(x, y, width, height));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLong(IntPtr hWnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLong(IntPtr hWnd, int index, IntPtr value);
}
