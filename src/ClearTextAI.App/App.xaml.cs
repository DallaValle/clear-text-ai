using ClearTextAI.App.Services;

namespace ClearTextAI.App;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
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

        return window;
    }
}
