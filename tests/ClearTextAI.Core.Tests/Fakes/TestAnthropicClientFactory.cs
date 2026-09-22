using Anthropic;
using ClearTextAI.Core.Models;
using ClearTextAI.Core.Services.AI;

namespace ClearTextAI.Core.Tests.Fakes;

/// <summary>Builds a real SDK client but routes it through a stub transport.</summary>
public sealed class TestAnthropicClientFactory : IAnthropicClientFactory
{
    private readonly HttpMessageHandler _handler;

    public TestAnthropicClientFactory(HttpMessageHandler handler) => _handler = handler;

    public string? LastApiKey { get; private set; }

    public AnthropicClient Create(AiProviderSettings settings, string apiKey)
    {
        LastApiKey = apiKey;

        return new AnthropicClient
        {
            ApiKey = apiKey,
            HttpClient = new HttpClient(_handler, disposeHandler: false),
            // No retries: the tests assert on the first mapped failure, not on backoff behaviour.
            MaxRetries = 0,
            Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds),
        };
    }
}
