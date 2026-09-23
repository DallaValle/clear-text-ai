namespace ClearTextAI.Core.Services.Quick;

/// <summary>
/// A system wide shortcut that works while the app is running but not focused.
/// Platforms that cannot offer one report <see cref="IsSupported"/> as false.
/// </summary>
public interface IGlobalHotkeyService
{
    bool IsSupported { get; }

    bool IsRegistered { get; }

    /// <summary>Human readable form of the shortcut, for the settings screen.</summary>
    string DisplayShortcut { get; }

    event EventHandler? Pressed;

    /// <summary>Returns false when the combination is already owned by another application.</summary>
    bool Register();

    void Unregister();
}
