using ClearTextAI.Core.Services.Quick;
using ClearTextAI.Core.Services.Settings;

namespace ClearTextAI.App.Services;

/// <summary>
/// Owns the lifetime of the global shortcut: binds it to the window, keeps it in step with
/// the setting, and routes a press to the coordinator.
/// </summary>
public sealed class QuickRewriteHost
{
    private readonly IGlobalHotkeyService _hotkey;
    private readonly QuickRewriteCoordinator _coordinator;
    private readonly ISettingsService _settings;

    private bool _started;

    public QuickRewriteHost(
        IGlobalHotkeyService hotkey,
        QuickRewriteCoordinator coordinator,
        ISettingsService settings)
    {
        _hotkey = hotkey;
        _coordinator = coordinator;
        _settings = settings;
    }

    public void Start(Window window)
    {
        if (_started || !_hotkey.IsSupported)
        {
            return;
        }

        _started = true;

#if WINDOWS
        if (_hotkey is Platforms.Windows.WindowsGlobalHotkeyService windowsHotkey
            && window.Handler?.PlatformView is Microsoft.UI.Xaml.Window platformWindow)
        {
            windowsHotkey.Attach(WinRT.Interop.WindowNative.GetWindowHandle(platformWindow));
        }
#endif

        _hotkey.Pressed += OnHotkeyPressed;
        _settings.SettingsChanged += (_, _) => ApplySetting();
        ApplySetting();
    }

    public void Stop()
    {
        _hotkey.Pressed -= OnHotkeyPressed;
        _hotkey.Unregister();
    }

    private void ApplySetting()
    {
        if (_settings.Current.GlobalShortcutEnabled)
        {
            _hotkey.Register();
        }
        else
        {
            _hotkey.Unregister();
        }
    }

    // Fire and forget on purpose: the OS message pump must not wait on a network call.
    private void OnHotkeyPressed(object? sender, EventArgs e) => _ = _coordinator.RunAsync();
}
