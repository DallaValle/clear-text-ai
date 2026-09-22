using ClearTextAI.Core.Services.Platform;

namespace ClearTextAI.App.Services;

public sealed class MauiLinkOpener : ILinkOpener
{
    public Task OpenAsync(string url) =>
        Browser.Default.OpenAsync(url, BrowserLaunchMode.SystemPreferred);
}
