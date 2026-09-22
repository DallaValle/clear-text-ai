using ClearTextAI.App.Services;
using ClearTextAI.Core.ViewModels;

namespace ClearTextAI.App.Views;

public partial class MainPage : ContentPage
{
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        KeyboardShortcuts.Assign(ImproveMenuItem, "Enter");
        KeyboardShortcuts.Assign(CopyMenuItem, "C", withShift: true);
        KeyboardShortcuts.Assign(ClearMenuItem, "L");
        KeyboardShortcuts.Assign(SettingsMenuItem, ",");
        KeyboardShortcuts.Assign(HistoryMenuItem, "H");
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        InputEditor.Focus();
    }
}
