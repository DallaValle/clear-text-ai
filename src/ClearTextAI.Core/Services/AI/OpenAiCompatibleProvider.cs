using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClearTextAI.Core.Models;
using ClearTextAI.Core.Services.Prompt;
using ClearTextAI.Core.Services.SecureStorage;
using ClearTextAI.Core.Services.Settings;

namespace ClearTextAI.Core.Services.AI;

/// <summary>
/// Chat Completions API. Covers OpenAI itself and anything that speaks the same shape:
/// gateways, local runtimes such as Ollama or LM Studio, self-hosted models.
/// </summary>
public sealed class OpenAiCompatibleProvider : IAiProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly IPromptBuilder _promptBuilder;
    private readonly IApiKeyStore _apiKeyStore;
    private readonly ISettingsService _settings;

    public OpenAiCompatibleProvider(
        AiProviderKind kind,
        HttpClient httpClient,
        IPromptBuilder promptBuilder,
        IApiKeyStore apiKeyStore,
        ISettingsService settings)
    {
        Kind = kind;
        _httpClient = httpClient;
        _promptBuilder = promptBuilder;
        _apiKeyStore = apiKeyStore;
        _settings = settings;
    }

    public AiProviderKind Kind { get; }

    public string DisplayName => Kind switch
    {
        AiProviderKind.OpenAi => "OpenAI",
        AiProviderKind.Grok => "Grok",
        _ => "the custom endpoint",
    };

    public async Task<string> RewriteTextAsync(
        string text,
        RewriteOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(text))
        {
            throw AiProviderException.For(AiErrorKind.EmptyInput, DisplayName);
        }

        var providerSettings = _settings.Current.GetOrCreate(Kind);
        var baseUrl = ResolveBaseUrl(providerSettings);

        if (string.IsNullOrWhiteSpace(providerSettings.Model))
        {
            throw AiProviderException.For(AiErrorKind.MissingModel, DisplayName);
        }

        var apiKey = await _apiKeyStore.GetAsync(Kind, cancellationToken).ConfigureAwait(false);

        // A local runtime usually needs no key, so only the hosted providers require one.
        if (apiKey is null && AiProviderSettings.RequiresApiKey(Kind))
        {
            throw AiProviderException.For(AiErrorKind.MissingApiKey, DisplayName);
        }

        var prompt = _promptBuilder.Build(text, options);
        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions")
        {
            Content = JsonContent.Create(
                new ChatCompletionRequest(
                    providerSettings.Model,
                    [
                        new ChatMessage("system", prompt.System),
                        new ChatMessage("user", prompt.User),
                    ],
                    options.Temperature,
                    providerSettings.MaxOutputTokens),
                options: SerializerOptions),
        };

        if (apiKey is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(providerSettings.TimeoutSeconds));

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw AiProviderException.For(AiErrorKind.Cancelled, DisplayName);
        }
        catch (OperationCanceledException ex)
        {
            throw AiProviderException.For(AiErrorKind.Timeout, DisplayName, ex);
        }
        catch (HttpRequestException ex)
        {
            throw AiProviderException.For(AiErrorKind.Network, DisplayName, ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw AiProviderException.For(MapStatusCode(response.StatusCode), DisplayName);
            }

            ChatCompletionResponse? payload;
            try
            {
                payload = await response.Content
                    .ReadFromJsonAsync<ChatCompletionResponse>(SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Cancelled while the body was still streaming in.
                throw AiProviderException.For(AiErrorKind.Cancelled, DisplayName);
            }
            catch (OperationCanceledException ex)
            {
                throw AiProviderException.For(AiErrorKind.Timeout, DisplayName, ex);
            }
            catch (Exception ex) when (ex is JsonException or HttpRequestException)
            {
                throw AiProviderException.For(AiErrorKind.Unknown, DisplayName, ex);
            }

            var choice = payload?.Choices?.FirstOrDefault();
            var content = choice?.Message?.Content?.Trim();

            if (string.IsNullOrEmpty(content))
            {
                throw AiProviderException.For(AiErrorKind.EmptyResponse, DisplayName);
            }

            if (string.Equals(choice?.FinishReason, "length", StringComparison.OrdinalIgnoreCase))
            {
                throw AiProviderException.For(AiErrorKind.ResponseTruncated, DisplayName);
            }

            return content;
        }
    }

    private string ResolveBaseUrl(AiProviderSettings providerSettings)
    {
        var endpoint = providerSettings.Endpoint;

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = AiProviderSettings.DefaultEndpoint(Kind);
        }

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw AiProviderException.For(AiErrorKind.MissingEndpoint, DisplayName);
        }

        return endpoint.TrimEnd('/');
    }

    private static AiErrorKind MapStatusCode(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized => AiErrorKind.InvalidApiKey,
        // A valid key with no credits answers 403, which is a very different fix for the user.
        HttpStatusCode.Forbidden or HttpStatusCode.PaymentRequired => AiErrorKind.AccessDenied,
        HttpStatusCode.TooManyRequests => AiErrorKind.RateLimited,
        HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => AiErrorKind.Timeout,
        HttpStatusCode.NotFound or HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity =>
            AiErrorKind.InvalidRequest,
        >= HttpStatusCode.InternalServerError => AiErrorKind.ProviderUnavailable,
        _ => AiErrorKind.Unknown,
    };

    private sealed record ChatCompletionRequest(
        string Model,
        IReadOnlyList<ChatMessage> Messages,
        double Temperature,
        [property: JsonPropertyName("max_tokens")] int MaxTokens);

    private sealed record ChatMessage(string Role, string Content);

    private sealed record ChatCompletionResponse(IReadOnlyList<ChatChoice>? Choices);

    private sealed record ChatChoice(
        ChatMessage? Message,
        [property: JsonPropertyName("finish_reason")] string? FinishReason);
}
