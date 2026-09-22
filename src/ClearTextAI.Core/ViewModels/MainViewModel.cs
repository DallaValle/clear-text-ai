using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClearTextAI.Core.Models;
using ClearTextAI.Core.Services.AI;
using ClearTextAI.Core.Services.Platform;
using ClearTextAI.Core.Services.Settings;

namespace ClearTextAI.Core.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IRewriteService _rewriteService;
    private readonly ISettingsService _settings;
    private readonly IClipboardService _clipboard;
    private readonly INavigationService _navigation;

    public MainViewModel(
        IRewriteService rewriteService,
        ISettingsService settings,
        IClipboardService clipboard,
        INavigationService navigation)
    {
        _rewriteService = rewriteService;
        _settings = settings;
        _clipboard = clipboard;
        _navigation = navigation;

        Presets = new ObservableCollection<DisplayOption<RewritePreset>>(
            RewritePresets.All.Select(p => new DisplayOption<RewritePreset>(p, RewritePresets.DisplayName(p))));
        _selectedPreset = FindPreset(settings.Current.RewriteOptions.Preset);
        _settings.SettingsChanged += OnSettingsChanged;
    }

    public ObservableCollection<DisplayOption<RewritePreset>> Presets { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CharacterCount))]
    [NotifyPropertyChangedFor(nameof(WordCount))]
    [NotifyPropertyChangedFor(nameof(CountsLabel))]
    [NotifyPropertyChangedFor(nameof(HasInput))]
    [NotifyCanExecuteChangedFor(nameof(ImproveTextCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearInputCommand))]
    private string _inputText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOutput))]
    [NotifyCanExecuteChangedFor(nameof(CopyOutputCommand))]
    [NotifyCanExecuteChangedFor(nameof(UseOutputAsInputCommand))]
    private string _outputText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ImproveTextCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string? _statusMessage;

    [ObservableProperty]
    private DisplayOption<RewritePreset> _selectedPreset;

    public int CharacterCount => InputText.Length;

    public int WordCount => CountWords(InputText);

    public string CountsLabel => $"{CharacterCount} characters, {WordCount} words";

    public bool HasInput => !string.IsNullOrWhiteSpace(InputText);

    public bool HasOutput => !string.IsNullOrWhiteSpace(OutputText);

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

    /// <summary>Shown next to the action button so it is always clear when text leaves the machine.</summary>
    public string PrivacyNotice =>
        $"Your text is sent to {ActiveProviderName} only when you press Improve Text.";

    public string ActiveProviderName => _settings.Current.ActiveProvider switch
    {
        AiProviderKind.Claude => "Claude",
        AiProviderKind.OpenAi => "OpenAI",
        AiProviderKind.Grok => "Grok",
        _ => "the configured endpoint",
    };

    partial void OnSelectedPresetChanged(DisplayOption<RewritePreset> value)
    {
        if (_settings.Current.RewriteOptions.Preset == value.Value)
        {
            return;
        }

        RewritePresets.Apply(value.Value, _settings.Current.RewriteOptions);
        _ = _settings.SaveAsync();
    }

    [RelayCommand(CanExecute = nameof(CanImproveText), IncludeCancelCommand = true)]
    private async Task ImproveTextAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;

        try
        {
            var result = await _rewriteService.RewriteAsync(InputText, cancellationToken).ConfigureAwait(true);
            OutputText = result.RewrittenText;
            StatusMessage = $"Improved in {result.Duration.TotalSeconds:0.0}s with {result.Model}.";
        }
        catch (AiProviderException ex) when (ex.Kind == AiErrorKind.Cancelled)
        {
            StatusMessage = "Cancelled.";
        }
        catch (AiProviderException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cancelled.";
        }
        catch (Exception)
        {
            // Last resort: an unexpected exception must never reach the user as a stack trace.
            ErrorMessage = "Something went wrong while improving the text. Try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanImproveText() => !IsBusy && HasInput;

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private async Task CopyOutputAsync()
    {
        await _clipboard.SetTextAsync(OutputText).ConfigureAwait(true);
        StatusMessage = "Copied to clipboard.";
    }

    [RelayCommand(CanExecute = nameof(HasInput))]
    private void ClearInput()
    {
        InputText = string.Empty;
        OutputText = string.Empty;
        ErrorMessage = null;
        StatusMessage = null;
    }

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private void UseOutputAsInput()
    {
        InputText = OutputText;
        OutputText = string.Empty;
        StatusMessage = "Result moved to the input.";
    }

    [RelayCommand]
    private Task OpenSettingsAsync() => _navigation.GoToAsync(AppRoutes.Settings);

    [RelayCommand]
    private Task OpenHistoryAsync() => _navigation.GoToAsync(AppRoutes.History);

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        SelectedPreset = FindPreset(_settings.Current.RewriteOptions.Preset);
        OnPropertyChanged(nameof(ActiveProviderName));
        OnPropertyChanged(nameof(PrivacyNotice));
    }

    private DisplayOption<RewritePreset> FindPreset(RewritePreset preset) =>
        Presets.First(p => p.Value == preset);

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var count = 0;
        var inWord = false;

        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                inWord = false;
            }
            else if (!inWord)
            {
                inWord = true;
                count++;
            }
        }

        return count;
    }
}
