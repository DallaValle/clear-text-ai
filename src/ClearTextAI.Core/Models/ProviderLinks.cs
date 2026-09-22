namespace ClearTextAI.Core.Models;

/// <summary>Where a user goes to create an API key for each provider.</summary>
public static class ProviderLinks
{
    public const string ClaudeApiKeys = "https://platform.claude.com/settings/keys";
    public const string OpenAiApiKeys = "https://platform.openai.com/api-keys";
    public const string GrokApiKeys = "https://console.x.ai/team/default/api-keys";

    /// <summary>Null for providers that have no key page, such as a local model.</summary>
    public static string? ApiKeyPageFor(AiProviderKind provider) => provider switch
    {
        AiProviderKind.Claude => ClaudeApiKeys,
        AiProviderKind.OpenAi => OpenAiApiKeys,
        AiProviderKind.Grok => GrokApiKeys,
        _ => null,
    };
}
