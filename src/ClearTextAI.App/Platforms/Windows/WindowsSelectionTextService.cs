using System.Diagnostics;
using System.Runtime.InteropServices;
using ClearTextAI.Core.Services.Quick;

namespace ClearTextAI.App.Platforms.Windows;

/// <summary>
/// Reads and replaces the selection in the foreground application by driving the clipboard,
/// which is the only technique that works across every Windows app. The user's own clipboard
/// contents are put back afterwards.
/// </summary>
public sealed class WindowsSelectionTextService : ISelectionTextService
{
    private static readonly TimeSpan CaptureTimeout = TimeSpan.FromMilliseconds(900);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(40);

    /// <summary>Gives the foreground app a moment to act on the synthetic keystroke.</summary>
    private static readonly TimeSpan KeystrokeSettle = TimeSpan.FromMilliseconds(120);

    private const ushort VkControl = 0x11;
    private const ushort VkShift = 0x10;
    private const ushort VkAlt = 0x12;
    private const ushort VkLeftWin = 0x5B;
    private const ushort VkRightWin = 0x5C;
    private const ushort VkC = 0x43;
    private const ushort VkV = 0x56;

    private string? _savedClipboard;

    public bool IsSupported => true;

    public async Task<string?> CaptureSelectionAsync(CancellationToken cancellationToken = default)
    {
        _savedClipboard = await ReadClipboardAsync().ConfigureAwait(false);

        // Emptying first turns "nothing was copied" into an unambiguous answer.
        await WriteClipboardAsync(string.Empty).ConfigureAwait(false);

        ReleaseHotkeyModifiers();
        SendCtrlKey(VkC);

        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < CaptureTimeout)
        {
            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);

            var captured = await ReadClipboardAsync().ConfigureAwait(false);
            if (!string.IsNullOrEmpty(captured))
            {
                return captured;
            }
        }

        await RestoreClipboardAsync().ConfigureAwait(false);
        return null;
    }

    public async Task ReplaceSelectionAsync(string text, CancellationToken cancellationToken = default)
    {
        await WriteClipboardAsync(text).ConfigureAwait(false);

        ReleaseHotkeyModifiers();
        SendCtrlKey(VkV);

        await Task.Delay(KeystrokeSettle, cancellationToken).ConfigureAwait(false);
        await RestoreClipboardAsync().ConfigureAwait(false);
    }

    private async Task RestoreClipboardAsync()
    {
        if (_savedClipboard is not null)
        {
            await WriteClipboardAsync(_savedClipboard).ConfigureAwait(false);
        }

        _savedClipboard = null;
    }

    private static Task<string?> ReadClipboardAsync() =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                return await Clipboard.Default.GetTextAsync().ConfigureAwait(true);
            }
            catch (Exception)
            {
                // Another process can hold the clipboard open; treat that as "nothing there".
                return null;
            }
        });

    private static Task WriteClipboardAsync(string text) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                await Clipboard.Default.SetTextAsync(text).ConfigureAwait(true);
            }
            catch (Exception)
            {
            }
        });

    /// <summary>
    /// The shortcut leaves Ctrl and Alt physically down. Without lifting them the synthetic
    /// Ctrl+C would reach the target app as Ctrl+Alt+C.
    /// </summary>
    private static uint ReleaseHotkeyModifiers() =>
        Send(
            KeyUp(VkAlt),
            KeyUp(VkShift),
            KeyUp(VkLeftWin),
            KeyUp(VkRightWin),
            KeyUp(VkControl));

    private static uint SendCtrlKey(ushort key) =>
        Send(
            KeyDown(VkControl),
            KeyDown(key),
            KeyUp(key),
            KeyUp(VkControl));

    private static uint Send(params Input[] inputs) =>
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());

    private static Input KeyDown(ushort key) => Key(key, 0);

    private static Input KeyUp(ushort key) => Key(key, KeyEventKeyUp);

    private static Input Key(ushort key, uint flags) => new()
    {
        Type = InputKeyboard,
        Union = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = key, Flags = flags } },
    };

    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
        [FieldOffset(0)] public HardwareInput Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint Data;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput
    {
        public uint Message;
        public ushort ParamL;
        public ushort ParamH;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);
}
