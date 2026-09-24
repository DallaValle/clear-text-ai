using ClearTextAI.App.Services;
using ClearTextAI.Core.ViewModels;
#if WINDOWS
using ClearTextAI.App.Platforms.Windows;
#endif

namespace ClearTextAI.App.Views;

public partial class MainPage : ContentPage
{
#if WINDOWS
    private readonly WindowsBackgroundHost _background;
#endif

    public MainPage(
        MainViewModel viewModel
#if WINDOWS
        , WindowsBackgroundHost background
#endif
        )
    {
        InitializeComponent();
        BindingContext = viewModel;

        KeyboardShortcuts.Assign(ImproveMenuItem, "Enter");
        KeyboardShortcuts.Assign(CopyMenuItem, "C", withShift: true);
        KeyboardShortcuts.Assign(ClearMenuItem, "L");
        KeyboardShortcuts.Assign(SettingsMenuItem, ",");
        KeyboardShortcuts.Assign(HistoryMenuItem, "H");

#if WINDOWS
        // The title bar close hides the window, so quitting has to be available from inside it too.
        _background = background;
        var exit = new MenuFlyoutItem { Text = "Exit" };
        exit.Clicked += (_, _) => _background.Exit();
        var file = new MenuBarItem { Text = "File" };
        file.Add(exit);
        MenuBarItems.Insert(0, file);
#endif
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        InputEditor.Focus();
    }
}
