using Anthropic;
using ClearTextAI.Core.Models;

namespace ClearTextAI.Core.Services.AI;

/// <summary>Creates configured SDK clients. Exists so tests can substitute the transport.</summary>
public interface IAnthropicClientFactory
{
    AnthropicClient Create(AiProviderSettings settings, string apiKey);
}

public sealed class AnthropicClientFactory : IAnthropicClientFactory
{
    public const string DefaultBaseUrl = "https://api.anthropic.com";

    public AnthropicClient Create(AiProviderSettings settings, string apiKey)
    {
        var endpoint = settings.Endpoint?.Trim();

        return new AnthropicClient
        {
            ApiKey = apiKey,
            Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds),
            MaxRetries = 2,
            BaseUrl = string.IsNullOrEmpty(endpoint) ? DefaultBaseUrl : endpoint,
        };
    }
}
