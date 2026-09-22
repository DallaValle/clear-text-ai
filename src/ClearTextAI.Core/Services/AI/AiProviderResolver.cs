using ClearTextAI.Core.Models;
using ClearTextAI.Core.Services.Settings;

namespace ClearTextAI.Core.Services.AI;

public sealed class AiProviderResolver : IAiProviderResolver
{
    private readonly IReadOnlyDictionary<AiProviderKind, IAiProvider> _providers;
    private readonly ISettingsService _settings;

    public AiProviderResolver(IEnumerable<IAiProvider> providers, ISettingsService settings)
    {
        _providers = providers.ToDictionary(p => p.Kind);
        _settings = settings;
    }

    public IAiProvider Resolve() => Resolve(_settings.Current.ActiveProvider);

    public IAiProvider Resolve(AiProviderKind kind) =>
        _providers.TryGetValue(kind, out var provider)
            ? provider
            : throw AiProviderException.For(AiErrorKind.ProviderUnavailable, kind.ToString());
}
