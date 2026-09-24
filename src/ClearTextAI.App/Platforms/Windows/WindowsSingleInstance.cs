using System.Runtime.InteropServices;

namespace ClearTextAI.App.Platforms.Windows;

/// <summary>
/// One process owns Ctrl+Alt+I. A second launch asks that process to show its window, then leaves.
/// The mutex is held for the life of the process on purpose: dropping it would allow a second copy.
/// </summary>
internal static class WindowsSingleInstance
{
    private const string MutexName = @"Local\ClearTextAI.SingleInstance";
    private const string ReadyName = @"Local\ClearTextAI.WindowReady";

    private static readonly IntPtr HwndBroadcast = new(0xFFFF);

    private static Mutex? _mutex;
    private static EventWaitHandle? _ready;

    static WindowsSingleInstance() => ShowMessage = RegisterWindowMessage("ClearTextAI.Activate");

    /// <summary>Posted to the running instance. Zero when the system could not register it.</summary>
    internal static uint ShowMessage { get; }

    internal static bool TryAcquire()
    {
        try
        {
            _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
            if (!createdNew)
            {
                _mutex.Dispose();
                _mutex = null;
                return false;
            }
        }
        catch (AbandonedMutexException)
        {
            // The constructor throws away the object that owns the abandoned mutex.
            // Open it again and keep the reference, or a later collection would drop the lock.
            _mutex = Mutex.OpenExisting(MutexName);
            try
            {
                if (!_mutex.WaitOne(0))
                {
                    _mutex.Dispose();
                    _mutex = null;
                    return false;
                }
            }
            catch (AbandonedMutexException)
            {
                // WaitOne grants ownership and reports the previous owner's crash.
            }
        }

        _ready = new EventWaitHandle(false, EventResetMode.ManualReset, ReadyName);
        return true;
    }

    /// <summary>Called once the window is listening, so a second launch is not missed during startup.</summary>
    internal static void MarkReady() => _ready?.Set();

    internal static void SignalExisting()
    {
        using var ready = new EventWaitHandle(false, EventResetMode.ManualReset, ReadyName);
        ready.WaitOne(TimeSpan.FromSeconds(5));

        if (ShowMessage != 0)
        {
            PostMessage(HwndBroadcast, ShowMessage, IntPtr.Zero, IntPtr.Zero);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
