using ClearTextAI.Core.Services.Platform;

namespace ClearTextAI.App.Services;

public sealed class ShellNavigationService : INavigationService
{
    public Task GoToAsync(string route) =>
        MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(route));

    public Task GoBackAsync() =>
        MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(".."));
}
