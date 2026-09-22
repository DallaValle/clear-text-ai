using Microsoft.UI.Xaml;

// The WinUI entry point. Everything else lives in the shared project.
namespace ClearTextAI.App.WinUI;

public partial class App : MauiWinUIApplication
{
    public App() => InitializeComponent();

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
