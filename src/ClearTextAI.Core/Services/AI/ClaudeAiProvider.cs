using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using ClearTextAI.Core.Models;
using ClearTextAI.Core.Services.Prompt;
using ClearTextAI.Core.Services.SecureStorage;
using ClearTextAI.Core.Services.Settings;

namespace ClearTextAI.Core.Services.AI;

/// <summary>Claude / Anthropic Messages API, through the official SDK.</summary>
public sealed class ClaudeAiProvider : IAiProvider
{
    private readonly IAnthropicClientFactory _clientFactory;
    private readonly IPromptBuilder _promptBuilder;
    private readonly IApiKeyStore _apiKeyStore;
    private readonly ISettingsService _settings;

    public ClaudeAiProvider(
        IAnthropicClientFactory clientFactory,
        IPromptBuilder promptBuilder,
        IApiKeyStore apiKeyStore,
        ISettingsService settings)
    {
        _clientFactory = clientFactory;
        _promptBuilder = promptBuilder;
        _apiKeyStore = apiKeyStore;
        _settings = settings;
    }

    public AiProviderKind Kind => AiProviderKind.Claude;

    public string DisplayName => "Claude";

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

        var apiKey = await _apiKeyStore.GetAsync(Kind, cancellationToken).ConfigureAwait(false)
            ?? throw AiProviderException.For(AiErrorKind.MissingApiKey, DisplayName);

        var providerSettings = _settings.Current.GetOrCreate(Kind);

        if (string.IsNullOrWhiteSpace(providerSettings.Model))
        {
            throw AiProviderException.For(AiErrorKind.MissingModel, DisplayName);
        }

        var prompt = _promptBuilder.Build(text, options);
        var parameters = BuildParameters(prompt, providerSettings);
        var client = _clientFactory.Create(providerSettings, apiKey);

        Message response;
        try
        {
            response = await client.Messages.Create(parameters, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw AiProviderException.For(AiErrorKind.Cancelled, DisplayName);
        }
        catch (Exception ex)
        {
            throw Translate(ex, cancellationToken);
        }

        return ExtractText(response);
    }

    private static MessageCreateParams BuildParameters(
        RewritePrompt prompt,
        AiProviderSettings providerSettings)
    {
        // Claude no longer accepts a temperature parameter, so the creativity setting reaches
        // the model through the prompt instead. Rewriting is a shallow task, so low effort
        // keeps it fast and cheap where the model supports it.
        return new MessageCreateParams
        {
            Model = providerSettings.Model,
            MaxTokens = providerSettings.MaxOutputTokens,
            System = prompt.System,
            Messages = [new() { Role = Role.User, Content = prompt.User }],
            OutputConfig = SupportsEffort(providerSettings.Model)
                ? new OutputConfig { Effort = Effort.Low }
                : null,
        };
    }

    /// <summary>
    /// Older models reject output_config.effort. An id this build does not know is assumed to be
    /// newer than this build, so it is treated like the current generation.
    /// </summary>
    private static bool SupportsEffort(string model) =>
        !model.StartsWith("claude-haiku-4-5", StringComparison.OrdinalIgnoreCase)
        && !model.StartsWith("claude-sonnet-4-5", StringComparison.OrdinalIgnoreCase);

    private string ExtractText(Message response)
    {
        if (IsStopReason(response, "refusal"))
        {
            throw AiProviderException.For(AiErrorKind.Refused, DisplayName);
        }

        var text = string.Concat(response.Content
            .Select(block => block.Value)
            .OfType<TextBlock>()
            .Select(block => block.Text))
            .Trim();

        if (text.Length == 0)
        {
            throw AiProviderException.For(AiErrorKind.EmptyResponse, DisplayName);
        }

        if (IsStopReason(response, "max_tokens"))
        {
            throw AiProviderException.For(AiErrorKind.ResponseTruncated, DisplayName);
        }

        return text;
    }

    /// <summary>
    /// The SDK renders the stop reason as a JSON-quoted wire value ("max_tokens"). Comparing on
    /// letters only keeps this working whether it is quoted, snake_case or the enum name.
    /// </summary>
    private static bool IsStopReason(Message response, string wireName) =>
        string.Equals(LettersOnly(response.StopReason?.ToString()), LettersOnly(wireName), StringComparison.OrdinalIgnoreCase);

    private static string LettersOnly(string? value) =>
        value is null ? string.Empty : new string(value.Where(char.IsLetter).ToArray());

    /// <summary>
    /// Maps SDK failures onto user-facing errors. Nothing from the exception is surfaced:
    /// SDK messages can echo request details.
    /// </summary>
    private AiProviderException Translate(Exception ex, CancellationToken cancellationToken) => ex switch
    {
        AiProviderException known => known,
        AnthropicUnauthorizedException => AiProviderException.For(AiErrorKind.InvalidApiKey, DisplayName, ex),
        AnthropicForbiddenException => AiProviderException.For(AiErrorKind.AccessDenied, DisplayName, ex),
        AnthropicRateLimitException => AiProviderException.For(AiErrorKind.RateLimited, DisplayName, ex),
        AnthropicNotFoundException => AiProviderException.For(AiErrorKind.InvalidRequest, DisplayName, ex),
        AnthropicBadRequestException => AiProviderException.For(AiErrorKind.InvalidRequest, DisplayName, ex),
        AnthropicUnprocessableEntityException => AiProviderException.For(AiErrorKind.InvalidRequest, DisplayName, ex),
        Anthropic5xxException => AiProviderException.For(AiErrorKind.ProviderUnavailable, DisplayName, ex),
        TaskCanceledException or TimeoutException when !cancellationToken.IsCancellationRequested =>
            AiProviderException.For(AiErrorKind.Timeout, DisplayName, ex),
        OperationCanceledException => AiProviderException.For(AiErrorKind.Cancelled, DisplayName, ex),
        AnthropicIOException or HttpRequestException => AiProviderException.For(AiErrorKind.Network, DisplayName, ex),
        _ => AiProviderException.For(AiErrorKind.Unknown, DisplayName, ex),
    };
}
