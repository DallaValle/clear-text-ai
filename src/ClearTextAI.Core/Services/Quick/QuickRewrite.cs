namespace ClearTextAI.Core.Services.Quick;

/// <summary>Single source of truth for how the shortcut is spelled, shared by the UI and the platform code.</summary>
public static class QuickRewrite
{
    // Ctrl+Option+I is already taken on macOS, so the Mac uses the Cmd based combination.
    public static string ShortcutDisplay { get; } =
        OperatingSystem.IsMacCatalyst() || OperatingSystem.IsMacOS() ? "Cmd+Shift+I" : "Ctrl+Alt+I";
}
