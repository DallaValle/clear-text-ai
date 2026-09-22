using System.Net;
using ClearTextAI.Core.Models;
using ClearTextAI.Core.Services.AI;
using ClearTextAI.Core.Services.Prompt;
using ClearTextAI.Core.Services.SecureStorage;
using ClearTextAI.Core.Tests.Fakes;
using Xunit;

namespace ClearTextAI.Core.Tests;

public class ClaudeAiProviderTests
{
    private const string TestApiKey = "sk-ant-test-0123456789-SECRET";

    private static string MessageJson(string text, string stopReason = "end_turn") => $$"""
        {
          "id": "msg_test",
          "type": "message",
          "role": "assistant",
          "model": "claude-opus-5",
          "content": [{ "type": "text", "text": "{{text}}" }],
          "stop_reason": "{{stopReason}}",
          "stop_sequence": null,
          "usage": { "input_tokens": 12, "output_tokens": 7 }
        }
        """;

    private static string ErrorJson(string type) => $$"""
        { "type": "error", "error": { "type": "{{type}}", "message": "something went wrong" } }
        """;

    private static (ClaudeAiProvider Provider, FakeSecureCredentialStore Credentials, StubHttpMessageHandler Handler)
        CreateProvider(StubHttpMessageHandler handler, bool withApiKey = true)
    {
        var credentials = new FakeSecureCredentialStore();
        var apiKeyStore = new ApiKeyStore(credentials);

        if (withApiKey)
        {
            apiKeyStore.SetAsync(AiProviderKind.Claude, TestApiKey).GetAwaiter().GetResult();
        }

        var provider = new ClaudeAiProvider(
            new TestAnthropicClientFactory(handler),
            new PromptBuilder(),
            apiKeyStore,
            new InMemorySettingsService());

        return (provider, credentials, handler);
    }

    private static Task<string> RewriteAsync(StubHttpMessageHandler handler, bool withApiKey = true)
    {
        var (provider, _, _) = CreateProvider(handler, withApiKey);
        return provider.RewriteTextAsync("this sentence need fixing", RewritePresets.Create(RewritePreset.Natural));
    }

    private static async Task<AiProviderException> AssertFailsAsync(StubHttpMessageHandler handler, bool withApiKey = true) =>
        await Assert.ThrowsAsync<AiProviderException>(() => RewriteAsync(handler, withApiKey));

    [Fact]
    public void Provider_ExposesItsKindAndName()
    {
        var (provider, _, _) = CreateProvider(StubHttpMessageHandler.Json(HttpStatusCode.OK, MessageJson("ok")));

        Assert.Equal(AiProviderKind.Claude, provider.Kind);
        Assert.Equal("Claude", provider.DisplayName);
        Assert.IsAssignableFrom<IAiProvider>(provider);
    }

    [Fact]
    public async Task RewriteTextAsync_ReturnsOnlyTheRewrittenText()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, MessageJson("This sentence needs fixing."));

        var result = await RewriteAsync(handler);

        Assert.Equal("This sentence needs fixing.", result);
    }

    [Fact]
    public async Task RewriteTextAsync_SendsTheBuiltPromptAndTheConfiguredModel()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, MessageJson("ok"));

        await RewriteAsync(handler);

        var body = Assert.Single(handler.RequestBodies);
        Assert.Contains(ModelCatalog.DefaultModelFor(AiProviderKind.Claude), body);
        Assert.Contains("text_to_rewrite", body);
        Assert.Contains("this sentence need fixing", body);
        // Claude rejects sampling parameters, so the request must not carry one.
        Assert.DoesNotContain("\"temperature\"", body);
    }

    [Fact]
    public async Task RewriteTextAsync_SendsTheApiKeyAsAHeaderOnly()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, MessageJson("ok"));

        await RewriteAsync(handler);

        var request = Assert.Single(handler.Requests);
        Assert.True(request.Headers.TryGetValues("x-api-key", out var values));
        Assert.Equal(TestApiKey, Assert.Single(values!));
        Assert.DoesNotContain(TestApiKey, Assert.Single(handler.RequestBodies));
    }

    [Fact]
    public async Task RewriteTextAsync_WithoutStoredKey_ReportsMissingApiKey()
    {
        var error = await AssertFailsAsync(
            StubHttpMessageHandler.Json(HttpStatusCode.OK, MessageJson("ok")),
            withApiKey: false);

        Assert.Equal(AiErrorKind.MissingApiKey, error.Kind);
        Assert.Contains("Settings", error.Message);
    }

    [Fact]
    public async Task RewriteTextAsync_WithEmptyInput_ReportsEmptyInput()
    {
        var (provider, _, _) = CreateProvider(StubHttpMessageHandler.Json(HttpStatusCode.OK, MessageJson("ok")));

        var error = await Assert.ThrowsAsync<AiProviderException>(
            () => provider.RewriteTextAsync("   ", new RewriteOptions()));

        Assert.Equal(AiErrorKind.EmptyInput, error.Kind);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, AiErrorKind.InvalidApiKey)]
    [InlineData(HttpStatusCode.Forbidden, AiErrorKind.AccessDenied)]
    [InlineData(HttpStatusCode.TooManyRequests, AiErrorKind.RateLimited)]
    [InlineData(HttpStatusCode.BadRequest, AiErrorKind.InvalidRequest)]
    [InlineData(HttpStatusCode.NotFound, AiErrorKind.InvalidRequest)]
    [InlineData(HttpStatusCode.InternalServerError, AiErrorKind.ProviderUnavailable)]
    [InlineData(HttpStatusCode.ServiceUnavailable, AiErrorKind.ProviderUnavailable)]
    public async Task RewriteTextAsync_MapsHttpFailuresToUsefulErrors(HttpStatusCode status, AiErrorKind expected)
    {
        var error = await AssertFailsAsync(StubHttpMessageHandler.Json(status, ErrorJson("api_error")));

        Assert.Equal(expected, error.Kind);
    }

    [Fact]
    public async Task RewriteTextAsync_OnNetworkFailure_ReportsAConnectionProblem()
    {
        var handler = StubHttpMessageHandler.Throws(new HttpRequestException("no such host"));

        var error = await AssertFailsAsync(handler);

        Assert.Equal(AiErrorKind.Network, error.Kind);
        Assert.Contains("internet connection", error.Message);
        Assert.True(error.IsTransient);
    }

    [Fact]
    public async Task RewriteTextAsync_OnTimeout_ReportsATimeout()
    {
        var handler = StubHttpMessageHandler.Throws(new TaskCanceledException("timed out"));

        var error = await AssertFailsAsync(handler);

        Assert.Equal(AiErrorKind.Timeout, error.Kind);
    }

    [Fact]
    public async Task RewriteTextAsync_WhenCancelled_ReportsCancellation()
    {
        var (provider, _, _) = CreateProvider(StubHttpMessageHandler.Json(HttpStatusCode.OK, MessageJson("ok")));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var error = await Assert.ThrowsAsync<AiProviderException>(
            () => provider.RewriteTextAsync("some text", new RewriteOptions(), cts.Token));

        Assert.Equal(AiErrorKind.Cancelled, error.Kind);
    }

    [Fact]
    public async Task RewriteTextAsync_WithEmptyContent_ReportsAnEmptyResponse()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, MessageJson("   "));

        var error = await AssertFailsAsync(handler);

        Assert.Equal(AiErrorKind.EmptyResponse, error.Kind);
    }

    [Fact]
    public async Task RewriteTextAsync_WhenTruncated_SaysTheOutputLimitWasHit()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, MessageJson("half a sent", "max_tokens"));

        var error = await AssertFailsAsync(handler);

        Assert.Equal(AiErrorKind.ResponseTruncated, error.Kind);
    }

    [Fact]
    public async Task RewriteTextAsync_WhenRefused_SaysSo()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, MessageJson("", "refusal"));

        var error = await AssertFailsAsync(handler);

        Assert.Equal(AiErrorKind.Refused, error.Kind);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task ErrorMessages_NeverLeakTheApiKeyOrTheUsersText(HttpStatusCode status)
    {
        var error = await AssertFailsAsync(StubHttpMessageHandler.Json(status, ErrorJson("api_error")));

        Assert.DoesNotContain(TestApiKey, error.ToString());
        Assert.DoesNotContain("sk-ant", error.Message);
        Assert.DoesNotContain("this sentence need fixing", error.Message);
    }

    [Fact]
    public async Task CredentialStoreFailure_IsReportedWithoutPlatformDetails()
    {
        var credentials = new FakeSecureCredentialStore { ThrowOnGet = true };
        var provider = new ClaudeAiProvider(
            new TestAnthropicClientFactory(StubHttpMessageHandler.Json(HttpStatusCode.OK, MessageJson("ok"))),
            new PromptBuilder(),
            new ApiKeyStore(credentials),
            new InMemorySettingsService());

        var error = await Assert.ThrowsAsync<AiProviderException>(
            () => provider.RewriteTextAsync("text", new RewriteOptions()));

        Assert.Equal(AiErrorKind.CredentialStoreUnavailable, error.Kind);
        Assert.DoesNotContain("credential store offline", error.Message);
    }
}
