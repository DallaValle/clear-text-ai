using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClearTextAI.Core.Models;
using ClearTextAI.Core.Services.AI;
using ClearTextAI.Core.Services.Platform;
using ClearTextAI.Core.Services.SecureStorage;
using ClearTextAI.Core.Services.Settings;

namespace ClearTextAI.Core.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private const string ConnectionTestSentence = "this sentence are a test of the connection";

    private readonly ISettingsService _settings;
    private readonly IApiKeyStore _apiKeyStore;
    private readonly IAiProviderResolver _resolver;
    private readonly IDialogService _dialogs;
    private readonly ILinkOpener _links;

    /// <summary>Suppresses the "switch to Custom" reaction while the form is being populated.</summary>
    private bool _isLoading;

    public SettingsViewModel(
        ISettingsService settings,
        IApiKeyStore apiKeyStore,
        IAiProviderResolver resolver,
        IDialogService dialogs,
        ILinkOpener links)
    {
        _settings = settings;
        _apiKeyStore = apiKeyStore;
        _resolver = resolver;
        _dialogs = dialogs;
        _links = links;

        Providers =
        [
            new DisplayOption<AiProviderKind>(AiProviderKind.Claude, "Claude (Anthropic)"),
            new DisplayOption<AiProviderKind>(AiProviderKind.OpenAi, "OpenAI"),
            new DisplayOption<AiProviderKind>(AiProviderKind.Grok, "Grok (xAI)"),
            new DisplayOption<AiProviderKind>(AiProviderKind.OpenAiCompatible, "Custom / OpenAI-compatible"),
        ];

        Tones =
        [
            new DisplayOption<RewriteTone>(RewriteTone.PreserveOriginal, "Preserve original tone"),
            new DisplayOption<RewriteTone>(RewriteTone.Friendly, "Friendly"),
            new DisplayOption<RewriteTone>(RewriteTone.Professional, "Professional"),
            new DisplayOption<RewriteTone>(RewriteTone.Casual, "Casual"),
            new DisplayOption<RewriteTone>(RewriteTone.Formal, "Formal"),
            new DisplayOption<RewriteTone>(RewriteTone.Concise, "Concise"),
        ];

        Presets = new ObservableCollection<DisplayOption<RewritePreset>>(
            RewritePresets.All.Select(p => new DisplayOption<RewritePreset>(p, RewritePresets.DisplayName(p))));

        SuggestedModels = new ObservableCollection<ModelOption>(ModelCatalog.SuggestedFor(AiProviderKind.Claude));

        _selectedProvider = Providers[0];
        _selectedTone = Tones[0];
        _selectedPreset = Presets[0];
    }

    public IReadOnlyList<DisplayOption<AiProviderKind>> Providers { get; }

    public IReadOnlyList<DisplayOption<RewriteTone>> Tones { get; }

    public ObservableCollection<DisplayOption<RewritePreset>> Presets { get; }

    public ObservableCollection<ModelOption> SuggestedModels { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSuggestedModels))]
    [NotifyPropertyChangedFor(nameof(EndpointPlaceholder))]
    [NotifyPropertyChangedFor(nameof(ApiKeyRequired))]
    [NotifyPropertyChangedFor(nameof(HasApiKeyPage))]
    [NotifyPropertyChangedFor(nameof(ApiKeyPageLabel))]
    [NotifyPropertyChangedFor(nameof(TemperatureIsUsed))]
    [NotifyPropertyChangedFor(nameof(TemperatureHint))]
    private DisplayOption<AiProviderKind> _selectedProvider;

    [ObservableProperty]
    private string _model = string.Empty;

    [ObservableProperty]
    private ModelOption? _selectedSuggestedModel;

    [ObservableProperty]
    private string _endpoint = string.Empty;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ApiKeyStatus))]
    private bool _hasStoredApiKey;

    [ObservableProperty]
    private int _maxOutputTokens = AiProviderSettings.DefaultMaxOutputTokens;

    [ObservableProperty]
    private int _timeoutSeconds = AiProviderSettings.DefaultTimeoutSeconds;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemperatureLabel))]
    private double _temperature = RewriteOptions.DefaultTemperature;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StyleLabel))]
    private double _styleLevel = (int)WritingStyle.HumanProfessional;

    [ObservableProperty]
    private DisplayOption<RewriteTone> _selectedTone;

    [ObservableProperty]
    private bool _preserveMeaning = true;

    [ObservableProperty]
    private bool _grammarOnly;

    [ObservableProperty]
    private string _customInstructions = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PresetDescription))]
    private DisplayOption<RewritePreset> _selectedPreset;

    [ObservableProperty]
    private bool _historyEnabled = true;

    [ObservableProperty]
    private bool _telemetryEnabled;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    private string? _statusMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    private string? _errorMessage;

    public bool HasSuggestedModels => SuggestedModels.Count > 0;

    public bool ApiKeyRequired => AiProviderSettings.RequiresApiKey(SelectedProvider.Value);

    /// <summary>False for a custom endpoint, which has no key page to send the user to.</summary>
    public bool HasApiKeyPage => ProviderLinks.ApiKeyPageFor(SelectedProvider.Value) is not null;

    public string ApiKeyPageLabel => SelectedProvider.Value switch
    {
        AiProviderKind.Claude => "Get a key from the Anthropic Console",
        AiProviderKind.OpenAi => "Get a key from the OpenAI dashboard",
        AiProviderKind.Grok => "Get a key from the xAI Console",
        _ => string.Empty,
    };

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    /// <summary>Drives the pinned footer, which must not take up space when there is nothing to say.</summary>
    public bool HasMessage => HasStatus || HasError;

    public string ApiKeyStatus => HasStoredApiKey
        ? "A key is stored in the system credential store."
        : "No key stored yet.";

    public string EndpointPlaceholder => AiProviderSettings.DefaultEndpoint(SelectedProvider.Value) is { Length: > 0 } url
        ? $"Leave empty for {url}"
        : "http://localhost:11434/v1";

    public string PresetDescription => RewritePresets.Description(SelectedPreset.Value);

    public string TemperatureLabel => $"{Temperature:0.0}";

    public string StyleLabel => ((WritingStyle)(int)Math.Round(StyleLevel)) switch
    {
        WritingStyle.Conversational => "Very natural and conversational",
        WritingStyle.HumanProfessional => "Human and professional",
        WritingStyle.Neutral => "Neutral",
        _ => "Highly polished and formal",
    };

    /// <summary>Claude no longer accepts a sampling parameter; other providers still do.</summary>
    public bool TemperatureIsUsed => SelectedProvider.Value != AiProviderKind.Claude;

    public string TemperatureHint => TemperatureIsUsed
        ? "Higher values allow more rephrasing."
        : "Claude models no longer accept a temperature parameter, so this setting is applied through the prompt instead.";

    [RelayCommand]
    public async Task LoadAsync()
    {
        _isLoading = true;
        try
        {
            var settings = _settings.Current;
            SelectedProvider = Providers.First(p => p.Value == settings.ActiveProvider);

            var providerSettings = settings.ActiveProviderSettings;
            Model = providerSettings.Model;
            RefreshSuggestedModels(settings.ActiveProvider);
            Endpoint = providerSettings.Endpoint;
            MaxOutputTokens = providerSettings.MaxOutputTokens;
            TimeoutSeconds = providerSettings.TimeoutSeconds;

            var options = settings.RewriteOptions;
            Temperature = options.Temperature;
            StyleLevel = (int)options.Style;
            SelectedTone = Tones.First(t => t.Value == options.Tone);
            PreserveMeaning = options.PreserveMeaning;
            GrammarOnly = options.GrammarOnly;
            CustomInstructions = options.CustomInstructions;
            SelectedPreset = Presets.First(p => p.Value == options.Preset);

            HistoryEnabled = settings.HistoryEnabled;
            TelemetryEnabled = settings.TelemetryEnabled;

            ApiKey = string.Empty;
            HasStoredApiKey = await _apiKeyStore.HasKeyAsync(settings.ActiveProvider).ConfigureAwait(true);
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>
    /// Each section saves on its own. Only that section's fields are written, so saving the
    /// provider never silently persists half-edited rewrite options, and the other way round.
    /// </summary>
    [RelayCommand]
    private Task SaveProviderAsync() => SaveSectionAsync(
        async () =>
        {
            ApplyProviderToSettings();
            await _settings.SaveAsync().ConfigureAwait(true);
            await StoreApiKeyIfProvidedAsync().ConfigureAwait(true);
        },
        "Provider settings saved.");

    [RelayCommand]
    private Task SaveRewriteOptionsAsync() => SaveSectionAsync(
        async () =>
        {
            ApplyRewriteOptionsToSettings();
            await _settings.SaveAsync().ConfigureAwait(true);
        },
        "Rewrite options saved.");

    [RelayCommand]
    private Task SavePrivacyAsync() => SaveSectionAsync(
        async () =>
        {
            ApplyPrivacyToSettings();
            await _settings.SaveAsync().ConfigureAwait(true);
        },
        "Privacy settings saved.");

    private async Task SaveSectionAsync(Func<Task> save, string successMessage)
    {
        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;

        try
        {
            await save().ConfigureAwait(true);
            StatusMessage = successMessage;
        }
        catch (AiProviderException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception)
        {
            ErrorMessage = "The settings could not be saved. Check that the app can write to your local app data folder.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task StoreApiKeyIfProvidedAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            return;
        }

        await _apiKeyStore.SetAsync(SelectedProvider.Value, ApiKey, cancellationToken).ConfigureAwait(true);
        ApiKey = string.Empty;
        HasStoredApiKey = true;
    }

    [RelayCommand(CanExecute = nameof(HasApiKeyPage))]
    private async Task OpenApiKeyPageAsync()
    {
        if (ProviderLinks.ApiKeyPageFor(SelectedProvider.Value) is { } url)
        {
            await _links.OpenAsync(url).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task RemoveApiKeyAsync()
    {
        if (!await _dialogs.ConfirmAsync("Remove API key", $"Remove the stored {SelectedProvider.Name} key from this machine?", "Remove").ConfigureAwait(true))
        {
            return;
        }

        await _apiKeyStore.RemoveAsync(SelectedProvider.Value).ConfigureAwait(true);
        ApiKey = string.Empty;
        HasStoredApiKey = false;
        StatusMessage = "API key removed.";
    }

    [RelayCommand]
    private async Task TestConnectionAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;

        try
        {
            // Save the provider section first so the test uses exactly what the user just typed.
            // Rewrite options are deliberately untouched: this button belongs to the provider card.
            ApplyProviderToSettings();
            await _settings.SaveAsync(cancellationToken).ConfigureAwait(true);
            await StoreApiKeyIfProvidedAsync(cancellationToken).ConfigureAwait(true);

            var provider = _resolver.Resolve(SelectedProvider.Value);
            await provider.RewriteTextAsync(
                ConnectionTestSentence,
                RewritePresets.Create(RewritePreset.GrammarOnly),
                cancellationToken).ConfigureAwait(true);

            StatusMessage = $"Connected to {provider.DisplayName} successfully.";
        }
        catch (AiProviderException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Test cancelled.";
        }
        catch (Exception)
        {
            ErrorMessage = "The connection test failed. Check the provider settings and try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyProviderToSettings()
    {
        var settings = _settings.Current;
        settings.ActiveProvider = SelectedProvider.Value;

        var providerSettings = settings.GetOrCreate(SelectedProvider.Value);
        providerSettings.Model = Model;
        providerSettings.Endpoint = Endpoint;
        providerSettings.MaxOutputTokens = MaxOutputTokens;
        providerSettings.TimeoutSeconds = TimeoutSeconds;
    }

    private void ApplyRewriteOptionsToSettings()
    {
        var options = _settings.Current.RewriteOptions;
        options.Temperature = Temperature;
        options.Style = (WritingStyle)(int)Math.Round(StyleLevel);
        options.Tone = SelectedTone.Value;
        options.PreserveMeaning = PreserveMeaning;
        options.GrammarOnly = GrammarOnly;
        options.CustomInstructions = CustomInstructions;
        options.Preset = SelectedPreset.Value;
    }

    private void ApplyPrivacyToSettings()
    {
        _settings.Current.HistoryEnabled = HistoryEnabled;
        _settings.Current.TelemetryEnabled = TelemetryEnabled;
    }

    partial void OnSelectedPresetChanged(DisplayOption<RewritePreset> value)
    {
        if (_isLoading || value.Value == RewritePreset.Custom)
        {
            return;
        }

        _isLoading = true;
        try
        {
            var applied = RewritePresets.Create(value.Value);
            Temperature = applied.Temperature;
            StyleLevel = (int)applied.Style;
            SelectedTone = Tones.First(t => t.Value == applied.Tone);
            PreserveMeaning = applied.PreserveMeaning;
            GrammarOnly = applied.GrammarOnly;
        }
        finally
        {
            _isLoading = false;
        }
    }

    partial void OnSelectedProviderChanged(DisplayOption<AiProviderKind> value)
    {
        if (_isLoading)
        {
            return;
        }

        var providerSettings = _settings.Current.GetOrCreate(value.Value);
        Model = providerSettings.Model;
        RefreshSuggestedModels(value.Value);
        Endpoint = providerSettings.Endpoint;
        ApiKey = string.Empty;
        OpenApiKeyPageCommand.NotifyCanExecuteChanged();
        _ = RefreshStoredKeyAsync(value.Value);
    }

    private async Task RefreshStoredKeyAsync(AiProviderKind provider) =>
        HasStoredApiKey = await _apiKeyStore.HasKeyAsync(provider).ConfigureAwait(true);

    /// <summary>The picker is a shortcut: the model id entry stays the value that is actually sent.</summary>
    partial void OnSelectedSuggestedModelChanged(ModelOption? value)
    {
        if (value is not null)
        {
            Model = value.Id;
        }
    }

    private void RefreshSuggestedModels(AiProviderKind provider)
    {
        SuggestedModels.Clear();
        foreach (var model in ModelCatalog.SuggestedFor(provider))
        {
            SuggestedModels.Add(model);
        }

        SelectedSuggestedModel = ModelCatalog.Find(provider, Model);
        OnPropertyChanged(nameof(HasSuggestedModels));
    }

    partial void OnTemperatureChanged(double value) => MarkCustom();

    partial void OnStyleLevelChanged(double value) => MarkCustom();

    partial void OnSelectedToneChanged(DisplayOption<RewriteTone> value) => MarkCustom();

    partial void OnPreserveMeaningChanged(bool value) => MarkCustom();

    partial void OnGrammarOnlyChanged(bool value) => MarkCustom();

    /// <summary>Any manual change means the options no longer match the chosen preset.</summary>
    private void MarkCustom()
    {
        if (_isLoading || SelectedPreset.Value == RewritePreset.Custom)
        {
            return;
        }

        SelectedPreset = Presets.First(p => p.Value == RewritePreset.Custom);
    }
}
