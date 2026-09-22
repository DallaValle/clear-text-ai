using System.Net;
using ClearTextAI.Core.Models;
using ClearTextAI.Core.Services.AI;
using ClearTextAI.Core.Services.Prompt;
using ClearTextAI.Core.Services.SecureStorage;
using ClearTextAI.Core.Tests.Fakes;
using Xunit;

namespace ClearTextAI.Core.Tests;

public class OpenAiCompatibleProviderTests
{
    private const string TestApiKey = "sk-openai-test-SECRET";

    private static string CompletionJson(string text, string finishReason = "stop") => $$"""
        {
          "id": "chatcmpl-test",
          "choices": [{ "index": 0, "finish_reason": "{{finishReason}}",
                        "message": { "role": "assistant", "content": "{{text}}" } }]
        }
        """;

    private static (OpenAiCompatibleProvider Provider, StubHttpMessageHandler Handler) Create(
        StubHttpMessageHandler handler,
        AiProviderKind kind = AiProviderKind.OpenAi,
        bool withApiKey = true,
        string endpoint = "")
    {
        var credentials = new FakeSecureCredentialStore();
        var apiKeyStore = new ApiKeyStore(credentials);

        if (withApiKey)
        {
            apiKeyStore.SetAsync(kind, TestApiKey).GetAwaiter().GetResult();
        }

        var settings = new InMemorySettingsService();
        settings.Current.ActiveProvider = kind;
        settings.Current.GetOrCreate(kind).Endpoint = endpoint;
        settings.Current.GetOrCreate(kind).Model = "test-model";

        var provider = new OpenAiCompatibleProvider(
            kind,
            new HttpClient(handler, disposeHandler: false),
            new PromptBuilder(),
            apiKeyStore,
            settings);

        return (provider, handler);
    }

    [Fact]
    public async Task RewriteTextAsync_ReturnsTheAssistantMessage()
    {
        var (provider, _) = Create(StubHttpMessageHandler.Json(HttpStatusCode.OK, CompletionJson("A clearer sentence.")));

        var result = await provider.RewriteTextAsync("a sentence", new RewriteOptions());

        Assert.Equal("A clearer sentence.", result);
    }

    [Fact]
    public async Task RewriteTextAsync_PostsToChatCompletionsWithABearerToken()
    {
        var (provider, handler) = Create(StubHttpMessageHandler.Json(HttpStatusCode.OK, CompletionJson("ok")));

        await provider.RewriteTextAsync("a sentence", new RewriteOptions { Temperature = 0.42 });

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://api.openai.com/v1/chat/completions", request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal(TestApiKey, request.Headers.Authorization.Parameter);

        var body = Assert.Single(handler.RequestBodies);
        Assert.Contains("\"temperature\":0.42", body);
        Assert.Contains("\"max_tokens\"", body);
        Assert.Contains("text_to_rewrite", body);
    }

    [Fact]
    public async Task RewriteTextAsync_ForGrok_PostsToTheXaiEndpointWithABearerToken()
    {
        var (provider, handler) = Create(
            StubHttpMessageHandler.Json(HttpStatusCode.OK, CompletionJson("A clearer sentence.")),
            AiProviderKind.Grok);

        var result = await provider.RewriteTextAsync("a sentence", new RewriteOptions { Temperature = 0.3 });

        Assert.Equal("A clearer sentence.", result);
        Assert.Equal("Grok", provider.DisplayName);
        Assert.Equal(AiProviderKind.Grok, provider.Kind);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://api.x.ai/v1/chat/completions", request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal(TestApiKey, request.Headers.Authorization.Parameter);
        Assert.Contains("text_to_rewrite", Assert.Single(handler.RequestBodies));
    }

    [Fact]
    public async Task RewriteTextAsync_ForGrokWithoutKey_ReportsMissingApiKey()
    {
        var (provider, _) = Create(
            StubHttpMessageHandler.Json(HttpStatusCode.OK, CompletionJson("ok")),
            AiProviderKind.Grok,
            withApiKey: false);

        var error = await Assert.ThrowsAsync<AiProviderException>(
            () => provider.RewriteTextAsync("a sentence", new RewriteOptions()));

        Assert.Equal(AiErrorKind.MissingApiKey, error.Kind);
        Assert.Contains("Grok", error.Message);
    }

    [Fact]
    public async Task RewriteTextAsync_ForGrok_HonoursACustomEndpointOverride()
    {
        var (provider, handler) = Create(
            StubHttpMessageHandler.Json(HttpStatusCode.OK, CompletionJson("ok")),
            AiProviderKind.Grok,
            endpoint: "https://gateway.internal/xai/v1");

        await provider.RewriteTextAsync("a sentence", new RewriteOptions());

        Assert.Equal("https://gateway.internal/xai/v1/chat/completions", Assert.Single(handler.Requests).RequestUri!.ToString());
    }

    [Fact]
    public async Task RewriteTextAsync_UsesTheConfiguredCustomEndpoint()
    {
        var (provider, handler) = Create(
            StubHttpMessageHandler.Json(HttpStatusCode.OK, CompletionJson("ok")),
            AiProviderKind.OpenAiCompatible,
            withApiKey: false,
            endpoint: "http://localhost:11434/v1/");

        await provider.RewriteTextAsync("a sentence", new RewriteOptions());

        var request = Assert.Single(handler.Requests);
        Assert.Equal("http://localhost:11434/v1/chat/completions", request.RequestUri!.ToString());
        // A local runtime normally has no key, so none must be required.
        Assert.Null(request.Headers.Authorization);
    }

    [Fact]
    public async Task RewriteTextAsync_CustomProviderWithoutEndpoint_AsksForConfiguration()
    {
        var (provider, _) = Create(
            StubHttpMessageHandler.Json(HttpStatusCode.OK, CompletionJson("ok")),
            AiProviderKind.OpenAiCompatible,
            withApiKey: false);

        var error = await Assert.ThrowsAsync<AiProviderException>(
            () => provider.RewriteTextAsync("a sentence", new RewriteOptions()));

        Assert.Equal(AiErrorKind.MissingEndpoint, error.Kind);
        Assert.Contains("Settings", error.Message);
    }

    [Fact]
    public async Task RewriteTextAsync_WithoutAModel_SaysSo()
    {
        var (provider, _) = Create(StubHttpMessageHandler.Json(HttpStatusCode.OK, CompletionJson("ok")));
        var settings = new InMemorySettingsService();
        settings.Current.GetOrCreate(AiProviderKind.OpenAi).Model = string.Empty;
        var withoutModel = new OpenAiCompatibleProvider(
            AiProviderKind.OpenAi,
            new HttpClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, CompletionJson("ok"))),
            new ClearTextAI.Core.Services.Prompt.PromptBuilder(),
            new ApiKeyStore(new FakeSecureCredentialStore()),
            settings);

        var error = await Assert.ThrowsAsync<AiProviderException>(
            () => withoutModel.RewriteTextAsync("a sentence", new RewriteOptions()));

        Assert.Equal(AiErrorKind.MissingModel, error.Kind);
    }

    [Fact]
    public async Task RewriteTextAsync_OpenAiWithoutKey_ReportsMissingApiKey()
    {
        var (provider, _) = Create(
            StubHttpMessageHandler.Json(HttpStatusCode.OK, CompletionJson("ok")),
            withApiKey: false);

        var error = await Assert.ThrowsAsync<AiProviderException>(
            () => provider.RewriteTextAsync("a sentence", new RewriteOptions()));

        Assert.Equal(AiErrorKind.MissingApiKey, error.Kind);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, AiErrorKind.InvalidApiKey)]
    [InlineData(HttpStatusCode.Forbidden, AiErrorKind.AccessDenied)]
    [InlineData(HttpStatusCode.PaymentRequired, AiErrorKind.AccessDenied)]
    [InlineData(HttpStatusCode.TooManyRequests, AiErrorKind.RateLimited)]
    [InlineData(HttpStatusCode.BadRequest, AiErrorKind.InvalidRequest)]
    [InlineData(HttpStatusCode.BadGateway, AiErrorKind.ProviderUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout, AiErrorKind.Timeout)]
    public async Task RewriteTextAsync_MapsHttpFailures(HttpStatusCode status, AiErrorKind expected)
    {
        var (provider, _) = Create(StubHttpMessageHandler.Json(status, """{"error":{"message":"nope"}}"""));

        var error = await Assert.ThrowsAsync<AiProviderException>(
            () => provider.RewriteTextAsync("a sentence", new RewriteOptions()));

        Assert.Equal(expected, error.Kind);
        Assert.DoesNotContain(TestApiKey, error.Message);
    }

    /// <summary>xAI answers 403 with "team doesn't have any credits", which is not a bad key.</summary>
    [Fact]
    public async Task RewriteTextAsync_WhenTheAccountHasNoCredits_DoesNotBlameTheApiKey()
    {
        var (provider, _) = Create(
            StubHttpMessageHandler.Json(HttpStatusCode.Forbidden,
                """{"code":"permission-denied","error":"Your newly created team doesn't have any credits or licenses yet."}"""),
            AiProviderKind.Grok);

        var error = await Assert.ThrowsAsync<AiProviderException>(
            () => provider.RewriteTextAsync("a sentence", new RewriteOptions()));

        Assert.Equal(AiErrorKind.AccessDenied, error.Kind);
        Assert.Contains("credits", error.Message);
        Assert.DoesNotContain("rejected the API key", error.Message);
    }

    [Fact]
    public async Task RewriteTextAsync_OnNetworkFailure_ReportsAConnectionProblem()
    {
        var (provider, _) = Create(StubHttpMessageHandler.Throws(new HttpRequestException("dns")));

        var error = await Assert.ThrowsAsync<AiProviderException>(
            () => provider.RewriteTextAsync("a sentence", new RewriteOptions()));

        Assert.Equal(AiErrorKind.Network, error.Kind);
    }

    [Fact]
    public async Task RewriteTextAsync_WhenTruncated_SaysTheOutputLimitWasHit()
    {
        var (provider, _) = Create(StubHttpMessageHandler.Json(HttpStatusCode.OK, CompletionJson("cut off", "length")));

        var error = await Assert.ThrowsAsync<AiProviderException>(
            () => provider.RewriteTextAsync("a sentence", new RewriteOptions()));

        Assert.Equal(AiErrorKind.ResponseTruncated, error.Kind);
    }

    [Fact]
    public async Task RewriteTextAsync_WithNoChoices_ReportsAnEmptyResponse()
    {
        var (provider, _) = Create(StubHttpMessageHandler.Json(HttpStatusCode.OK, """{"choices":[]}"""));

        var error = await Assert.ThrowsAsync<AiProviderException>(
            () => provider.RewriteTextAsync("a sentence", new RewriteOptions()));

        Assert.Equal(AiErrorKind.EmptyResponse, error.Kind);
    }

    [Fact]
    public async Task RewriteTextAsync_WhenCancelled_ReportsCancellation()
    {
        var (provider, _) = Create(StubHttpMessageHandler.Json(HttpStatusCode.OK, CompletionJson("ok")));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var error = await Assert.ThrowsAsync<AiProviderException>(
            () => provider.RewriteTextAsync("a sentence", new RewriteOptions(), cts.Token));

        Assert.Equal(AiErrorKind.Cancelled, error.Kind);
    }
}
