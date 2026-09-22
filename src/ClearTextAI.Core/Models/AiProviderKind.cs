namespace ClearTextAI.Core.Models;

/// <summary>Providers the app knows how to talk to. New providers are added here and in the DI registration.</summary>
public enum AiProviderKind
{
    Claude = 0,
    OpenAi = 1,

    /// <summary>Any OpenAI-compatible endpoint: local LLM runtimes, gateways, self-hosted models.</summary>
    OpenAiCompatible = 2,

    /// <summary>xAI Grok, which speaks the OpenAI chat completions shape.</summary>
    Grok = 3,
}
