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

        Routing.RegisterRoute(AppRoutes.Settings, typeof(SettingsPage));
        Routing.RegisterRoute(AppRoutes.History, typeof(HistoryPage));
    }
}
