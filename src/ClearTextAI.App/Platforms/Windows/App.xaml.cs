using ClearTextAI.App.Platforms.Windows;
using Microsoft.UI.Xaml;

// The WinUI entry point. Everything else lives in the shared project.
namespace ClearTextAI.App.WinUI;

public partial class App : MauiWinUIApplication
{
    private readonly bool _ownsInstance;

    public App()
    {
        _ownsInstance = WindowsSingleInstance.TryAcquire();
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (!_ownsInstance)
        {
            WindowsSingleInstance.SignalExisting();
            Environment.Exit(0);
            return;
        }

        base.OnLaunched(args);
    }
}
