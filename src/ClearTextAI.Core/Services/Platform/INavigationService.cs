namespace ClearTextAI.Core.Services.Platform;

public interface INavigationService
{
    Task GoToAsync(string route);

    Task GoBackAsync();
}

public static class AppRoutes
{
    public const string Settings = "settings";
    public const string History = "history";
}
