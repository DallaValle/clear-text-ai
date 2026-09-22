using ClearTextAI.Core.Models;
using ClearTextAI.Core.Services.AI;
using ClearTextAI.Core.Tests.Fakes;
using Xunit;

namespace ClearTextAI.Core.Tests;

public class RewriteServiceTests
{
    /// <summary>A provider stand-in, proving the abstraction is all the app layer depends on.</summary>
    private sealed class FakeProvider : IAiProvider
    {
        private readonly Func<string, RewriteOptions, string> _rewrite;

        public FakeProvider(AiProviderKind kind, Func<string, RewriteOptions, string>? rewrite = null)
        {
            Kind = kind;
            _rewrite = rewrite ?? ((text, _) => text.ToUpperInvariant());
        }

        public AiProviderKind Kind { get; }

        public string DisplayName => Kind.ToString();

        public RewriteOptions? LastOptions { get; private set; }

        public Task<string> RewriteTextAsync(string text, RewriteOptions options, CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_rewrite(text, options));
        }
    }

    private static (RewriteService Service, FakeProvider Provider, InMemoryHistoryStore History, InMemorySettingsService Settings)
        Create(AiProviderKind active = AiProviderKind.Claude)
    {
        var settings = new InMemorySettingsService();
        settings.Current.ActiveProvider = active;
        var provider = new FakeProvider(active);
        var history = new InMemoryHistoryStore();
        var resolver = new AiProviderResolver([provider], settings);
        return (new RewriteService(resolver, settings, history), provider, history, settings);
    }

    [Fact]
    public async Task RewriteAsync_ReturnsTheProvidersTextWithMetadata()
    {
        var (service, _, _, _) = Create();

        var result = await service.RewriteAsync("hello there");

        Assert.Equal("HELLO THERE", result.RewrittenText);
        Assert.Equal("hello there", result.OriginalText);
        Assert.Equal(AiProviderKind.Claude, result.Provider);
        Assert.Equal(ModelCatalog.DefaultModelFor(AiProviderKind.Claude), result.Model);
        Assert.True(result.Duration >= TimeSpan.Zero);
    }

    [Fact]
    public async Task RewriteAsync_WithBlankInput_FailsBeforeCallingTheProvider()
    {
        var (service, provider, _, _) = Create();

        var error = await Assert.ThrowsAsync<AiProviderException>(() => service.RewriteAsync("   "));

        Assert.Equal(AiErrorKind.EmptyInput, error.Kind);
        Assert.Null(provider.LastOptions);
    }

    [Fact]
    public async Task RewriteAsync_RecordsHistoryWhenEnabled()
    {
        var (service, _, history, _) = Create();

        await service.RewriteAsync("hello");

        var entry = Assert.Single(history.Items);
        Assert.Equal("hello", entry.OriginalText);
        Assert.Equal("HELLO", entry.RewrittenText);
        Assert.Equal(ModelCatalog.DefaultModelFor(AiProviderKind.Claude), entry.Model);
    }

    [Fact]
    public async Task RewriteAsync_RecordsNothingWhenHistoryIsDisabled()
    {
        var (service, _, history, settings) = Create();
        settings.Current.HistoryEnabled = false;

        await service.RewriteAsync("hello");

        Assert.Empty(history.Items);
    }

    [Fact]
    public async Task RewriteAsync_SnapshotsOptionsSoLaterSettingsChangesDoNotRewriteHistory()
    {
        var (service, _, history, settings) = Create();
        settings.Current.RewriteOptions.Tone = RewriteTone.Friendly;

        var result = await service.RewriteAsync("hello");
        settings.Current.RewriteOptions.Tone = RewriteTone.Formal;

        Assert.Equal(RewriteTone.Friendly, result.Options.Tone);
        Assert.Equal(RewriteTone.Friendly, Assert.Single(history.Items).Options.Tone);
    }

    [Fact]
    public async Task RewriteAsync_NormalizesOptionsBeforeSending()
    {
        var (service, provider, _, _) = Create();
        var options = new RewriteOptions { Temperature = 9.9, CustomInstructions = "   trim me   " };

        await service.RewriteAsync("hello", options);

        Assert.Equal(RewriteOptions.MaxTemperature, provider.LastOptions!.Temperature);
        Assert.Equal("trim me", provider.LastOptions.CustomInstructions);
        // The caller's own instance must not be mutated.
        Assert.Equal(9.9, options.Temperature);
    }

    [Fact]
    public async Task RewriteAsync_PropagatesCancellation()
    {
        var (service, _, history, _) = Create();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RewriteAsync("hello", cts.Token));
        Assert.Empty(history.Items);
    }

    [Fact]
    public void Resolver_ReturnsTheActiveProvider()
    {
        var settings = new InMemorySettingsService();
        settings.Current.ActiveProvider = AiProviderKind.OpenAi;
        var resolver = new AiProviderResolver(
            [new FakeProvider(AiProviderKind.Claude), new FakeProvider(AiProviderKind.OpenAi)],
            settings);

        Assert.Equal(AiProviderKind.OpenAi, resolver.Resolve().Kind);
        Assert.Equal(AiProviderKind.Claude, resolver.Resolve(AiProviderKind.Claude).Kind);
    }

    [Fact]
    public void Resolver_ForAnUnregisteredProvider_ReportsItAsUnavailable()
    {
        var resolver = new AiProviderResolver([new FakeProvider(AiProviderKind.Claude)], new InMemorySettingsService());

        var error = Assert.Throws<AiProviderException>(() => resolver.Resolve(AiProviderKind.OpenAi));

        Assert.Equal(AiErrorKind.ProviderUnavailable, error.Kind);
    }
}
