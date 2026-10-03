using ClearTextAI.App.Services;

namespace ClearTextAI.App;

public partial class App : Application
{
    private readonly IServiceProvider _services;

#if MACCATALYST
    private Platforms.MacCatalyst.MacMenuBarItem? _menuBarItem;
#endif

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState) => BuildWindow();

    private Window BuildWindow()
    {
        var window = new Window(_services.GetRequiredService<AppShell>())
        {
            Title = "ClearText AI",
            Width = 1080,
            Height = 720,
            MinimumWidth = 720,
            MinimumHeight = 520,
        };

        // The shortcut needs a live window to receive its message, so bind it once the window exists.
        window.Created += (_, _) =>
        {
            _services.GetRequiredService<QuickRewriteHost>().Start(window);
#if WINDOWS
            // Keeps that same window alive after the user closes it, which is what the shortcut is delivered to.
            _services.GetRequiredService<Platforms.Windows.WindowsBackgroundHost>().Start(window);
#endif
        };

#if MACCATALYST
        // Closing the window leaves the app running in the menu bar, where the shortcut keeps working.
        window.Created += (_, _) =>
        {
            _menuBarItem ??= new Platforms.MacCatalyst.MacMenuBarItem(ShowWindow);
            _menuBarItem.Install();
        };
        window.Destroying += (_, _) =>
        {
            if (Windows.All(w => w == window))
            {
                Platforms.MacCatalyst.MacMenuBarItem.EnterBackground();
            }
        };
#endif

        return window;
    }

#if MACCATALYST
    private void ShowWindow()
    {
        Platforms.MacCatalyst.MacMenuBarItem.EnterForeground();

        if (Windows.Count == 0)
        {
            OpenWindow(BuildWindow());
        }
    }
#endif
}
