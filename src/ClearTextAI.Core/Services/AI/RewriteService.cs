using System.Diagnostics;
using ClearTextAI.Core.Models;
using ClearTextAI.Core.Services.History;
using ClearTextAI.Core.Services.Settings;

namespace ClearTextAI.Core.Services.AI;

public sealed class RewriteService : IRewriteService
{
    private readonly IAiProviderResolver _resolver;
    private readonly ISettingsService _settings;
    private readonly IHistoryStore _history;

    public RewriteService(IAiProviderResolver resolver, ISettingsService settings, IHistoryStore history)
    {
        _resolver = resolver;
        _settings = settings;
        _history = history;
    }

    public Task<RewriteResult> RewriteAsync(string text, CancellationToken cancellationToken = default) =>
        RewriteAsync(text, _settings.Current.RewriteOptions, cancellationToken);

    public async Task<RewriteResult> RewriteAsync(
        string text,
        RewriteOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(text))
        {
            throw AiProviderException.For(AiErrorKind.EmptyInput, "the AI provider");
        }

        // Snapshot the options so a settings change mid-request cannot alter what history records.
        var effectiveOptions = options.Clone().Normalize();
        var provider = _resolver.Resolve();
        var providerSettings = _settings.Current.GetOrCreate(provider.Kind);

        var stopwatch = Stopwatch.StartNew();
        var rewritten = await provider.RewriteTextAsync(text, effectiveOptions, cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        var result = new RewriteResult
        {
            OriginalText = text,
            RewrittenText = rewritten,
            Provider = provider.Kind,
            Model = providerSettings.Model,
            Options = effectiveOptions,
            Duration = stopwatch.Elapsed,
        };

        if (_settings.Current.HistoryEnabled)
        {
            await _history.AddAsync(RewriteHistoryItem.FromResult(result), cancellationToken).ConfigureAwait(false);
        }

        return result;
    }
}
