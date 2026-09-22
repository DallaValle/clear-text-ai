namespace ClearTextAI.Core.Models;

/// <summary>A model offered in the settings picker.</summary>
public sealed record ModelOption(string Id, string DisplayName, string Hint)
{
    public override string ToString() => DisplayName;
}

/// <summary>
/// Suggested models per provider. The picker is a shortcut only: the model id field stays
/// editable so a model released after this build can be typed in.
/// </summary>
public static class ModelCatalog
{
    public static readonly IReadOnlyList<ModelOption> Claude =
    [
        new("claude-opus-5", "Claude Opus 5", "Best quality."),
        new("claude-sonnet-5", "Claude Sonnet 5", "Balanced quality and cost."),
        new("claude-haiku-4-5", "Claude Haiku 4.5", "Fastest and cheapest."),
    ];

    public static readonly IReadOnlyList<ModelOption> OpenAi =
    [
        new("gpt-4.1", "GPT-4.1", "Best quality."),
        new("gpt-4.1-mini", "GPT-4.1 mini", "Fast and inexpensive."),
    ];

    public static readonly IReadOnlyList<ModelOption> Grok =
    [
        new("grok-4.7", "Grok 4.7", "Most capable."),
        new("grok-4.6", "Grok 4.6", "Previous generation."),
        new("grok-4.5", "Grok 4.5", "Older, cheaper."),
    ];

    public static IReadOnlyList<ModelOption> SuggestedFor(AiProviderKind provider) => provider switch
    {
        AiProviderKind.Claude => Claude,
        AiProviderKind.OpenAi => OpenAi,
        AiProviderKind.Grok => Grok,
        // A custom endpoint serves whatever the user runs, so there is nothing to suggest.
        _ => [],
    };

    public static string DefaultModelFor(AiProviderKind provider) =>
        SuggestedFor(provider).FirstOrDefault()?.Id ?? string.Empty;

    public static ModelOption? Find(AiProviderKind provider, string? modelId) =>
        SuggestedFor(provider).FirstOrDefault(m => string.Equals(m.Id, modelId, StringComparison.OrdinalIgnoreCase));
}
