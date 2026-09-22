using ClearTextAI.Core.Services.Platform;

namespace ClearTextAI.Core.Tests.Fakes;

public sealed class FakeClipboardService : IClipboardService
{
    public string? LastText { get; private set; }

    public Task SetTextAsync(string text)
    {
        LastText = text;
        return Task.CompletedTask;
    }
}

public sealed class FakeNavigationService : INavigationService
{
    public List<string> Routes { get; } = [];

    public Task GoToAsync(string route)
    {
        Routes.Add(route);
        return Task.CompletedTask;
    }

    public Task GoBackAsync()
    {
        Routes.Add("..");
        return Task.CompletedTask;
    }
}

public sealed class FakeLinkOpener : ILinkOpener
{
    public List<string> OpenedUrls { get; } = [];

    public Task OpenAsync(string url)
    {
        OpenedUrls.Add(url);
        return Task.CompletedTask;
    }
}

public sealed class FakeDialogService : IDialogService
{
    public bool ConfirmResult { get; set; } = true;

    public List<string> Prompts { get; } = [];

    public Task AlertAsync(string title, string message, string cancel = "OK")
    {
        Prompts.Add(title);
        return Task.CompletedTask;
    }

    public Task<bool> ConfirmAsync(string title, string message, string accept = "Yes", string cancel = "Cancel")
    {
        Prompts.Add(title);
        return Task.FromResult(ConfirmResult);
    }
}
