using ClearTextAI.Core.Services.Quick;

namespace ClearTextAI.App.Services;

/// <summary>Stand ins for platforms without a system wide shortcut yet. The app runs, the feature is simply off.</summary>
public sealed class UnsupportedGlobalHotkeyService : IGlobalHotkeyService
{
    public bool IsSupported => false;

    public bool IsRegistered => false;

    public string DisplayShortcut => string.Empty;

    public event EventHandler? Pressed
    {
        add { }
        remove { }
    }

    public bool Register() => false;

    public void Unregister()
    {
    }
}

public sealed class UnsupportedSelectionTextService : ISelectionTextService
{
    public bool IsSupported => false;

    public Task<string?> CaptureSelectionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);

    public Task ReplaceSelectionAsync(string text, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

public sealed class NoQuickRewriteOverlay : IQuickRewriteOverlay
{
    public Task ShowBusyAsync(string message) => Task.CompletedTask;

    public Task ShowMessageAsync(string message, bool isError) => Task.CompletedTask;

    public Task HideAsync() => Task.CompletedTask;
}
