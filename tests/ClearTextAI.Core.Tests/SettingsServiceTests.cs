using ClearTextAI.Core.Models;
using ClearTextAI.Core.Services.Settings;
using ClearTextAI.Core.Tests.Fakes;
using Xunit;

namespace ClearTextAI.Core.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly TempDataDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    [Fact]
    public async Task LoadAsync_WithNoFile_ReturnsUsableDefaults()
    {
        var settings = await new SettingsService(_directory.Paths).LoadAsync();

        Assert.Equal(AiProviderKind.Claude, settings.ActiveProvider);
        Assert.Equal(ModelCatalog.DefaultModelFor(AiProviderKind.Claude), settings.ActiveProviderSettings.Model);
        Assert.True(settings.RewriteOptions.PreserveMeaning);
        Assert.True(settings.HistoryEnabled);
        Assert.False(settings.TelemetryEnabled);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTrips()
    {
        var service = new SettingsService(_directory.Paths);
        await service.LoadAsync();
        service.Current.ActiveProvider = AiProviderKind.OpenAiCompatible;
        service.Current.GetOrCreate(AiProviderKind.OpenAiCompatible).Endpoint = "http://localhost:11434/v1";
        service.Current.RewriteOptions.Tone = RewriteTone.Concise;
        service.Current.RewriteOptions.CustomInstructions = "keep technical terms";
        service.Current.HistoryEnabled = false;
        await service.SaveAsync();

        var reloaded = await new SettingsService(_directory.Paths).LoadAsync();

        Assert.Equal(AiProviderKind.OpenAiCompatible, reloaded.ActiveProvider);
        Assert.Equal("http://localhost:11434/v1", reloaded.ActiveProviderSettings.Endpoint);
        Assert.Equal(RewriteTone.Concise, reloaded.RewriteOptions.Tone);
        Assert.Equal("keep technical terms", reloaded.RewriteOptions.CustomInstructions);
        Assert.False(reloaded.HistoryEnabled);
    }

    /// <summary>
    /// The global shortcut can fire before any page has opened, so the settings must already be
    /// the real ones by the time the service is constructed.
    /// </summary>
    [Fact]
    public async Task Constructor_ReadsTheStoredFileWithoutAnyoneCallingLoad()
    {
        var first = new SettingsService(_directory.Paths);
        first.Current.ActiveProvider = AiProviderKind.Grok;
        first.Current.GetOrCreate(AiProviderKind.Grok).Model = "grok-4.6";
        await first.SaveAsync();

        var restarted = new SettingsService(_directory.Paths);

        Assert.Equal(AiProviderKind.Grok, restarted.Current.ActiveProvider);
        Assert.Equal("grok-4.6", restarted.Current.ActiveProviderSettings.Model);
    }

    [Fact]
    public void Constructor_WithACorruptFile_StillGivesUsableDefaults()
    {
        File.WriteAllText(_directory.Paths.SettingsFilePath, "{ not json");

        var service = new SettingsService(_directory.Paths);

        Assert.Equal(AiProviderKind.Claude, service.Current.ActiveProvider);
    }

    [Fact]
    public async Task SaveAsync_NeverWritesAnApiKey()
    {
        var service = new SettingsService(_directory.Paths);
        await service.LoadAsync();
        await service.SaveAsync();

        var json = await File.ReadAllTextAsync(_directory.Paths.SettingsFilePath);

        Assert.DoesNotContain("apikey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sk-", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadAsync_WithACorruptFile_FallsBackToDefaults()
    {
        await File.WriteAllTextAsync(_directory.Paths.SettingsFilePath, "{ nope");

        var settings = await new SettingsService(_directory.Paths).LoadAsync();

        Assert.Equal(AiProviderKind.Claude, settings.ActiveProvider);
    }

    [Fact]
    public async Task LoadAsync_NormalizesValuesFromAHandEditedFile()
    {
        await File.WriteAllTextAsync(_directory.Paths.SettingsFilePath, """
            {
              "activeProvider": "Claude",
              "providers": [{ "provider": "Claude", "model": "", "timeoutSeconds": 99999, "maxOutputTokens": 1 }],
              "rewriteOptions": { "temperature": 8.5 }
            }
            """);

        var settings = await new SettingsService(_directory.Paths).LoadAsync();

        Assert.Equal(ModelCatalog.DefaultModelFor(AiProviderKind.Claude), settings.ActiveProviderSettings.Model);
        Assert.Equal(600, settings.ActiveProviderSettings.TimeoutSeconds);
        Assert.Equal(256, settings.ActiveProviderSettings.MaxOutputTokens);
        Assert.Equal(RewriteOptions.MaxTemperature, settings.RewriteOptions.Temperature);
    }

    [Fact]
    public async Task Normalize_GivesEveryKnownProviderAnEntry()
    {
        var settings = await new SettingsService(_directory.Paths).LoadAsync();

        foreach (var kind in Enum.GetValues<AiProviderKind>())
        {
            Assert.Contains(settings.Providers, p => p.Provider == kind);
        }
    }

    [Fact]
    public async Task SaveAsync_RaisesSettingsChanged()
    {
        var service = new SettingsService(_directory.Paths);
        var raised = 0;
        service.SettingsChanged += (_, _) => raised++;

        await service.LoadAsync();
        await service.SaveAsync();

        Assert.Equal(2, raised);
    }
}
