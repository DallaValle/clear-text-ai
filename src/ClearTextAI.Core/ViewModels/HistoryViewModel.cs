using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClearTextAI.Core.Models;
using ClearTextAI.Core.Services.History;
using ClearTextAI.Core.Services.Platform;

namespace ClearTextAI.Core.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly IHistoryStore _history;
    private readonly IClipboardService _clipboard;
    private readonly IDialogService _dialogs;

    public HistoryViewModel(IHistoryStore history, IClipboardService clipboard, IDialogService dialogs)
    {
        _history = history;
        _clipboard = clipboard;
        _dialogs = dialogs;
    }

    public ObservableCollection<RewriteHistoryItem> Items { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string? _statusMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isLoaded;

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool IsEmpty => IsLoaded && Items.Count == 0;

    [RelayCommand]
    public async Task LoadAsync()
    {
        var items = await _history.GetAllAsync().ConfigureAwait(true);

        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        IsLoaded = true;
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private async Task CopyAsync(RewriteHistoryItem? item)
    {
        if (item is null)
        {
            return;
        }

        await _clipboard.SetTextAsync(item.RewrittenText).ConfigureAwait(true);
        StatusMessage = "Copied to clipboard.";
    }

    [RelayCommand]
    private async Task DeleteAsync(RewriteHistoryItem? item)
    {
        if (item is null)
        {
            return;
        }

        await _history.DeleteAsync(item.Id).ConfigureAwait(true);
        Items.Remove(item);
        OnPropertyChanged(nameof(IsEmpty));
        StatusMessage = "Entry deleted.";
    }

    [RelayCommand]
    private async Task ClearAllAsync()
    {
        if (Items.Count == 0)
        {
            return;
        }

        if (!await _dialogs.ConfirmAsync("Clear history", "Delete every stored rewrite from this machine?", "Delete all").ConfigureAwait(true))
        {
            return;
        }

        await _history.ClearAsync().ConfigureAwait(true);
        Items.Clear();
        OnPropertyChanged(nameof(IsEmpty));
        StatusMessage = "History cleared.";
    }
}
