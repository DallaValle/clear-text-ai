using ClearTextAI.App.Views;
using ClearTextAI.Core.Services.Platform;

namespace ClearTextAI.App;

public partial class AppShell : Shell
{
    public AppShell(MainPage mainPage)
    {
        InitializeComponent();

        // Built here rather than through a ContentTemplate so pages come from the DI container.
        Items.Add(new ShellContent
        {
            Title = "ClearText AI",
            Route = "main",
            Content = mainPage,
        });

        RegisterRoutes();
    }

    private static bool _routesRegistered;

    // A shell is built per window, but routes are process wide and must be registered once.
    private static void RegisterRoutes()
    {
        if (_routesRegistered)
        {
            return;
        }

        _routesRegistered = true;
        Routing.RegisterRoute(AppRoutes.Settings, typeof(SettingsPage));
        Routing.RegisterRoute(AppRoutes.History, typeof(HistoryPage));
    }
}
