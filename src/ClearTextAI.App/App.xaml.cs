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

        return window;
    }
}
