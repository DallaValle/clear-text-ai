using ClearTextAI.Core.Services.Platform;

namespace ClearTextAI.App.Services;

public sealed class MauiClipboardService : IClipboardService
{
    public Task SetTextAsync(string text) => Clipboard.Default.SetTextAsync(text);
}
