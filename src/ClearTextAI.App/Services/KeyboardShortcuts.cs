namespace ClearTextAI.App.Services;

/// <summary>
/// Attaches desktop accelerators to menu items, using Ctrl on Windows and Cmd on macOS.
/// Menu items are the cross-platform way to register a shortcut in .NET MAUI.
/// </summary>
internal static class KeyboardShortcuts
{
#if MACCATALYST
    private const KeyboardAcceleratorModifiers PrimaryModifier = KeyboardAcceleratorModifiers.Cmd;
#else
    private const KeyboardAcceleratorModifiers PrimaryModifier = KeyboardAcceleratorModifiers.Ctrl;
#endif

    public static void Assign(MenuFlyoutItem item, string key, bool withShift = false)
    {
        var modifiers = withShift ? PrimaryModifier | KeyboardAcceleratorModifiers.Shift : PrimaryModifier;

        item.KeyboardAccelerators.Add(new KeyboardAccelerator
        {
            Modifiers = modifiers,
            Key = key,
        });
    }
}
