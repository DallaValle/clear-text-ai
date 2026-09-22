namespace ClearTextAI.Core.Models;

/// <summary>Per-provider connection settings. Never holds the API key - that lives in the OS credential store.</summary>
public sealed class AiProviderSettings
{
    public const int DefaultTimeoutSeconds = 90;
    public const int DefaultMaxOutputTokens = 16000;

    public AiProviderKind Provider { get; set; } = AiProviderKind.Claude;

    public string Model { get; set; } = string.Empty;

    /// <summary>Empty means "use the provider's default endpoint".</summary>
    public string Endpoint { get; set; } = string.Empty;

    public int MaxOutputTokens { get; set; } = DefaultMaxOutputTokens;

    public int TimeoutSeconds { get; set; } = DefaultTimeoutSeconds;

    public static AiProviderSettings CreateDefault(AiProviderKind provider) => new()
    {
        Provider = provider,
        Model = DefaultModel(provider),
        Endpoint = string.Empty,
        MaxOutputTokens = DefaultMaxOutputTokens,
        TimeoutSeconds = DefaultTimeoutSeconds,
    };

    public static string DefaultModel(AiProviderKind provider) => ModelCatalog.DefaultModelFor(provider);

    /// <summary>The endpoint used when the user leaves the field empty. Empty means the user must supply one.</summary>
    public static string DefaultEndpoint(AiProviderKind provider) => provider switch
    {
        AiProviderKind.Claude => "https://api.anthropic.com",
        AiProviderKind.OpenAi => "https://api.openai.com/v1",
        AiProviderKind.Grok => "https://api.x.ai/v1",
        _ => string.Empty,
    };

    /// <summary>A hosted provider always needs a key; a custom endpoint may be a local model that needs none.</summary>
    public static bool RequiresApiKey(AiProviderKind provider) => provider != AiProviderKind.OpenAiCompatible;

    public AiProviderSettings Clone() => new()
    {
        Provider = Provider,
        Model = Model,
        Endpoint = Endpoint,
        MaxOutputTokens = MaxOutputTokens,
        TimeoutSeconds = TimeoutSeconds,
    };

    public AiProviderSettings Normalize()
    {
        Model = (Model ?? string.Empty).Trim();
        Endpoint = (Endpoint ?? string.Empty).Trim();

        if (Model.Length == 0)
        {
            Model = DefaultModel(Provider);
        }

        MaxOutputTokens = Math.Clamp(MaxOutputTokens, 256, 128_000);
        TimeoutSeconds = Math.Clamp(TimeoutSeconds, 5, 600);
        return this;
    }
}
