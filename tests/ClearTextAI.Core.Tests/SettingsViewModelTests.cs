using ClearTextAI.Core.Models;
using ClearTextAI.Core.Services.AI;
using ClearTextAI.Core.Services.SecureStorage;
using ClearTextAI.Core.Tests.Fakes;
using ClearTextAI.Core.ViewModels;
using Xunit;

namespace ClearTextAI.Core.Tests;

public class SettingsViewModelTests
{
    private sealed class NoopProvider : IAiProvider
    {
        public AiProviderKind Kind { get; init; } = AiProviderKind.Claude;

        public string DisplayName => Kind.ToString();

        public Exception? Failure { get; set; }

        public Task<string> RewriteTextAsync(string text, RewriteOptions options, CancellationToken cancellationToken = default) =>
            Failure is null ? Task.FromResult("ok") : Task.FromException<string>(Failure);
    }

    private static (SettingsViewModel Vm, InMemorySettingsService Settings, ApiKeyStore Keys, NoopProvider Provider, FakeDialogService Dialogs, FakeLinkOpener Links, FakeClipboardService Clipboard)
        Create()
    {
        var settings = new InMemorySettingsService();
        var keys = new ApiKeyStore(new FakeSecureCredentialStore());
        var provider = new NoopProvider();
        var resolver = new AiProviderResolver([provider], settings);
        var dialogs = new FakeDialogService();
        var links = new FakeLinkOpener();
        var clipboard = new FakeClipboardService();
        return (new SettingsViewModel(settings, keys, resolver, dialogs, links, clipboard), settings, keys, provider, dialogs, links, clipboard);
    }

    [Fact]
    public async Task Load_ReflectsTheStoredSettings()
    {
        var (vm, settings, _, _, _, _, _) = Create();
        settings.Current.RewriteOptions.Tone = RewriteTone.Formal;
        settings.Current.RewriteOptions.CustomInstructions = "no marketing speak";

        await vm.LoadAsync();

        Assert.Equal(AiProviderKind.Claude, vm.SelectedProvider.Value);
        Assert.Equal(ModelCatalog.DefaultModelFor(AiProviderKind.Claude), vm.Model);
        Assert.Equal(RewriteTone.Formal, vm.SelectedTone.Value);
        Assert.Equal("no marketing speak", vm.CustomInstructions);
        Assert.False(vm.HasStoredApiKey);
    }

    [Fact]
    public async Task ChoosingAPreset_UpdatesEveryOptionItControls()
    {
        var (vm, _, _, _, _, _, _) = Create();
        await vm.LoadAsync();

        vm.SelectedPreset = vm.Presets.First(p => p.Value == RewritePreset.GrammarOnly);

        Assert.Equal(0.0, vm.Temperature);
        Assert.True(vm.GrammarOnly);
        Assert.Equal(RewriteTone.PreserveOriginal, vm.SelectedTone.Value);
        // Applying a preset must not be mistaken for a manual edit.
        Assert.Equal(RewritePreset.GrammarOnly, vm.SelectedPreset.Value);
    }

    [Fact]
    public async Task EditingAnOptionByHand_SwitchesToCustom()
    {
        var (vm, _, _, _, _, _, _) = Create();
        await vm.LoadAsync();
        vm.SelectedPreset = vm.Presets.First(p => p.Value == RewritePreset.Professional);

        vm.Temperature = 0.85;

        Assert.Equal(RewritePreset.Custom, vm.SelectedPreset.Value);
        Assert.Equal(0.85, vm.Temperature);
    }

    [Fact]
    public async Task Save_PersistsTheFormAndNormalizesIt()
    {
        var (vm, settings, _, _, _, _, _) = Create();
        await vm.LoadAsync();
        vm.Temperature = 0.65;
        vm.StyleLevel = 3;
        vm.SelectedTone = vm.Tones.First(t => t.Value == RewriteTone.Concise);
        vm.CustomInstructions = "  keep code untouched  ";
        vm.TimeoutSeconds = 5000;

        await vm.SaveRewriteOptionsCommand.ExecuteAsync(null);
        await vm.SaveProviderCommand.ExecuteAsync(null);

        var options = settings.Current.RewriteOptions;
        Assert.Equal(0.65, options.Temperature);
        Assert.Equal(WritingStyle.Polished, options.Style);
        Assert.Equal(RewriteTone.Concise, options.Tone);
        Assert.Equal("keep code untouched", options.CustomInstructions);
        Assert.Equal(600, settings.Current.ActiveProviderSettings.TimeoutSeconds);
        Assert.Equal("Provider settings saved.", vm.StatusMessage);
    }

    [Fact]
    public async Task Save_StoresTheApiKeyAndClearsTheField()
    {
        var (vm, _, keys, _, _, _, _) = Create();
        await vm.LoadAsync();
        vm.ApiKey = "sk-ant-test";

        await vm.SaveProviderCommand.ExecuteAsync(null);

        Assert.Equal("sk-ant-test", await keys.GetAsync(AiProviderKind.Claude));
        Assert.Empty(vm.ApiKey);
        Assert.True(vm.HasStoredApiKey);
    }

    [Fact]
    public async Task RemoveApiKey_AsksFirstAndDeletesTheKey()
    {
        var (vm, _, keys, _, dialogs, _, _) = Create();
        await keys.SetAsync(AiProviderKind.Claude, "sk-ant-test");
        await vm.LoadAsync();

        await vm.RemoveApiKeyCommand.ExecuteAsync(null);

        Assert.Contains("Remove API key", dialogs.Prompts);
        Assert.False(await keys.HasKeyAsync(AiProviderKind.Claude));
        Assert.False(vm.HasStoredApiKey);
    }

    [Fact]
    public async Task RemoveApiKey_DoesNothingWhenTheUserDeclines()
    {
        var (vm, _, keys, _, dialogs, _, _) = Create();
        await keys.SetAsync(AiProviderKind.Claude, "sk-ant-test");
        await vm.LoadAsync();
        dialogs.ConfirmResult = false;

        await vm.RemoveApiKeyCommand.ExecuteAsync(null);

        Assert.True(await keys.HasKeyAsync(AiProviderKind.Claude));
    }

    [Fact]
    public async Task TestConnection_ReportsSuccess()
    {
        var (vm, _, _, _, _, _, _) = Create();
        await vm.LoadAsync();

        await vm.TestConnectionCommand.ExecuteAsync(null);

        Assert.Contains("successfully", vm.StatusMessage);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task TestConnection_ShowsTheProviderMessageOnFailure()
    {
        var (vm, _, _, provider, _, _, _) = Create();
        await vm.LoadAsync();
        provider.Failure = AiProviderException.For(AiErrorKind.InvalidApiKey, "Claude");

        await vm.TestConnectionCommand.ExecuteAsync(null);

        Assert.Equal("Claude rejected the API key. Check the key in Settings, then try again.", vm.ErrorMessage);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task SwitchingProvider_LoadsThatProvidersOwnModelAndEndpoint()
    {
        var (vm, settings, _, _, _, _, _) = Create();
        settings.Current.GetOrCreate(AiProviderKind.OpenAiCompatible).Endpoint = "http://localhost:11434/v1";
        settings.Current.GetOrCreate(AiProviderKind.OpenAiCompatible).Model = "llama3";
        await vm.LoadAsync();

        vm.SelectedProvider = vm.Providers.First(p => p.Value == AiProviderKind.OpenAiCompatible);

        Assert.Equal("llama3", vm.Model);
        Assert.Equal("http://localhost:11434/v1", vm.Endpoint);
        Assert.True(vm.TemperatureIsUsed);
    }

    [Fact]
    public async Task ClaudeSelected_ExplainsThatTemperatureIsAppliedThroughThePrompt()
    {
        var (vm, _, _, _, _, _, _) = Create();
        await vm.LoadAsync();

        Assert.False(vm.TemperatureIsUsed);
        Assert.Contains("through the prompt", vm.TemperatureHint);
    }

    [Fact]
    public async Task ApiKeyLink_PointsAtTheSelectedProvidersKeyPage()
    {
        var (vm, _, _, _, _, links, _) = Create();
        await vm.LoadAsync();

        Assert.True(vm.HasApiKeyPage);
        Assert.Contains("Anthropic Console", vm.ApiKeyPageLabel);

        await vm.OpenApiKeyPageCommand.ExecuteAsync(null);

        Assert.Equal(ProviderLinks.ClaudeApiKeys, Assert.Single(links.OpenedUrls));
    }

    [Fact]
    public async Task ApiKeyLink_FollowsTheProviderSwitch()
    {
        var (vm, _, _, _, _, links, _) = Create();
        await vm.LoadAsync();

        vm.SelectedProvider = vm.Providers.First(p => p.Value == AiProviderKind.OpenAi);
        await vm.OpenApiKeyPageCommand.ExecuteAsync(null);

        Assert.Equal(ProviderLinks.OpenAiApiKeys, Assert.Single(links.OpenedUrls));
    }

    [Fact]
    public async Task ApiKeyLink_IsHiddenForACustomEndpoint()
    {
        var (vm, _, _, _, _, links, _) = Create();
        await vm.LoadAsync();

        vm.SelectedProvider = vm.Providers.First(p => p.Value == AiProviderKind.OpenAiCompatible);

        Assert.False(vm.HasApiKeyPage);
        Assert.False(vm.OpenApiKeyPageCommand.CanExecute(null));
        Assert.Empty(links.OpenedUrls);
    }

    [Fact]
    public void EveryKeyPageLinkIsAnHttpsUrl()
    {
        foreach (var provider in Enum.GetValues<AiProviderKind>())
        {
            if (ProviderLinks.ApiKeyPageFor(provider) is { } url)
            {
                Assert.StartsWith("https://", url);
                Assert.True(Uri.IsWellFormedUriString(url, UriKind.Absolute));
            }
        }
    }

    [Fact]
    public async Task SavingOneSection_LeavesTheOtherSectionsUntouched()
    {
        var (vm, settings, _, _, _, _, _) = Create();
        await vm.LoadAsync();

        // Edit both sections, then save only the provider one.
        vm.Model = "claude-haiku-4-5";
        vm.Temperature = 0.99;
        vm.CustomInstructions = "not saved yet";
        await vm.SaveProviderCommand.ExecuteAsync(null);

        Assert.Equal("claude-haiku-4-5", settings.Current.ActiveProviderSettings.Model);
        Assert.Equal(RewriteOptions.DefaultTemperature, settings.Current.RewriteOptions.Temperature);
        Assert.Empty(settings.Current.RewriteOptions.CustomInstructions);

        // Now the rewrite section, which must not disturb the provider values.
        await vm.SaveRewriteOptionsCommand.ExecuteAsync(null);

        Assert.Equal(0.99, settings.Current.RewriteOptions.Temperature);
        Assert.Equal("not saved yet", settings.Current.RewriteOptions.CustomInstructions);
        Assert.Equal("claude-haiku-4-5", settings.Current.ActiveProviderSettings.Model);
    }

    [Fact]
    public async Task SavingPrivacy_TouchesOnlyThePrivacyFlags()
    {
        var (vm, settings, _, _, _, _, _) = Create();
        await vm.LoadAsync();

        vm.HistoryEnabled = false;
        vm.TelemetryEnabled = true;
        vm.Temperature = 0.77;
        await vm.SavePrivacyCommand.ExecuteAsync(null);

        Assert.False(settings.Current.HistoryEnabled);
        Assert.True(settings.Current.TelemetryEnabled);
        Assert.Equal(RewriteOptions.DefaultTemperature, settings.Current.RewriteOptions.Temperature);
        Assert.Equal("Privacy settings saved.", vm.StatusMessage);
    }

    [Fact]
    public async Task TestConnection_DoesNotPersistUnsavedRewriteOptions()
    {
        var (vm, settings, _, _, _, _, _) = Create();
        await vm.LoadAsync();
        vm.Temperature = 0.91;

        await vm.TestConnectionCommand.ExecuteAsync(null);

        Assert.Equal(RewriteOptions.DefaultTemperature, settings.Current.RewriteOptions.Temperature);
    }

    [Fact]
    public async Task TestConnection_WhenItFails_KeepsTheTypedApiKeyInTheField()
    {
        var (vm, _, keys, provider, _, _, _) = Create();
        await vm.LoadAsync();
        vm.ApiKey = "sk-ant-typed-by-hand";
        provider.Failure = AiProviderException.For(AiErrorKind.AccessDenied, "Claude");

        await vm.TestConnectionCommand.ExecuteAsync(null);

        // The field keeps what the user pasted, so they can correct it instead of retyping.
        Assert.Equal("sk-ant-typed-by-hand", vm.ApiKey);
        Assert.True(vm.HasError);
        // It is still stored, because the test had to use it to find out that it failed.
        Assert.Equal("sk-ant-typed-by-hand", await keys.GetAsync(AiProviderKind.Claude));
    }

    [Fact]
    public async Task TestConnection_WhenItSucceeds_ClearsTheField()
    {
        var (vm, _, _, _, _, _, _) = Create();
        await vm.LoadAsync();
        vm.ApiKey = "sk-ant-good";

        await vm.TestConnectionCommand.ExecuteAsync(null);

        Assert.Empty(vm.ApiKey);
        Assert.True(vm.HasStoredApiKey);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task CopyError_PutsTheMessageOnTheClipboard()
    {
        var (vm, _, _, provider, _, _, clipboard) = Create();
        await vm.LoadAsync();
        provider.Failure = AiProviderException.For(AiErrorKind.Network, "Claude");
        await vm.TestConnectionCommand.ExecuteAsync(null);

        Assert.True(vm.CopyErrorCommand.CanExecute(null));
        await vm.CopyErrorCommand.ExecuteAsync(null);

        Assert.Equal("Unable to connect to Claude. Check your internet connection and try again.", clipboard.LastText);
    }

    [Fact]
    public async Task CopyError_IsUnavailableWhenThereIsNoError()
    {
        var (vm, _, _, _, _, _, _) = Create();
        await vm.LoadAsync();

        Assert.False(vm.CopyErrorCommand.CanExecute(null));
    }

    [Fact]
    public async Task Grok_UsesTheXaiDefaultsAndKeyPage()
    {
        var (vm, _, _, _, _, links, _) = Create();
        await vm.LoadAsync();

        vm.SelectedProvider = vm.Providers.First(p => p.Value == AiProviderKind.Grok);

        Assert.Equal("grok-4.7", vm.Model);
        Assert.True(vm.HasSuggestedModels);
        Assert.Contains(vm.SuggestedModels, m => m.Id == "grok-4.7");
        Assert.True(vm.ApiKeyRequired);
        Assert.True(vm.TemperatureIsUsed);
        Assert.Contains("api.x.ai", vm.EndpointPlaceholder);

        await vm.OpenApiKeyPageCommand.ExecuteAsync(null);
        Assert.Equal(ProviderLinks.GrokApiKeys, Assert.Single(links.OpenedUrls));
    }

    [Fact]
    public async Task CustomEndpoint_OffersNoSuggestedModelsAndNeedsNoKey()
    {
        var (vm, _, _, _, _, _, _) = Create();
        await vm.LoadAsync();

        vm.SelectedProvider = vm.Providers.First(p => p.Value == AiProviderKind.OpenAiCompatible);

        Assert.False(vm.HasSuggestedModels);
        Assert.Empty(vm.SuggestedModels);
        Assert.False(vm.ApiKeyRequired);
        // The field stays available: a custom endpoint is often a hosted service that needs a key.
        Assert.Contains("API key", vm.ApiKeyLabel);
        Assert.Contains("leave empty", vm.ApiKeyLabel);
    }

    [Fact]
    public async Task PickingASuggestedModel_UpdatesTheModelId()
    {
        var (vm, _, _, _, _, _, _) = Create();
        await vm.LoadAsync();

        vm.SelectedSuggestedModel = ModelCatalog.Claude.Last();

        Assert.Equal(ModelCatalog.Claude.Last().Id, vm.Model);
    }
}
