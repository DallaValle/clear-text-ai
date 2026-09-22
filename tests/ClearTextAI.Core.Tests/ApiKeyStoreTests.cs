using ClearTextAI.Core.Models;
using ClearTextAI.Core.Services.AI;
using ClearTextAI.Core.Services.SecureStorage;
using ClearTextAI.Core.Tests.Fakes;
using Xunit;

namespace ClearTextAI.Core.Tests;

public class ApiKeyStoreTests
{
    private readonly FakeSecureCredentialStore _credentials = new();
    private readonly ApiKeyStore _store;

    public ApiKeyStoreTests() => _store = new ApiKeyStore(_credentials);

    [Fact]
    public async Task Keys_AreStoredPerProvider()
    {
        await _store.SetAsync(AiProviderKind.Claude, "claude-key");
        await _store.SetAsync(AiProviderKind.OpenAi, "openai-key");

        Assert.Equal("claude-key", await _store.GetAsync(AiProviderKind.Claude));
        Assert.Equal("openai-key", await _store.GetAsync(AiProviderKind.OpenAi));
    }

    [Fact]
    public async Task GetAsync_TrimsStoredWhitespace()
    {
        await _credentials.SetAsync("cleartextai.apikey.claude", "  spaced-key  ");

        Assert.Equal("spaced-key", await _store.GetAsync(AiProviderKind.Claude));
    }

    [Fact]
    public async Task GetAsync_TreatsBlankAsMissing()
    {
        await _credentials.SetAsync("cleartextai.apikey.claude", "   ");

        Assert.Null(await _store.GetAsync(AiProviderKind.Claude));
        Assert.False(await _store.HasKeyAsync(AiProviderKind.Claude));
    }

    [Fact]
    public async Task RemoveAsync_DeletesTheKey()
    {
        await _store.SetAsync(AiProviderKind.Claude, "key");

        await _store.RemoveAsync(AiProviderKind.Claude);

        Assert.False(await _store.HasKeyAsync(AiProviderKind.Claude));
    }

    [Fact]
    public async Task SetAsync_RejectsABlankKey()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _store.SetAsync(AiProviderKind.Claude, "  "));
    }

    [Fact]
    public async Task GetAsync_WhenTheStoreFails_ThrowsAReadableError()
    {
        _credentials.ThrowOnGet = true;

        var error = await Assert.ThrowsAsync<AiProviderException>(() => _store.GetAsync(AiProviderKind.Claude));

        Assert.Equal(AiErrorKind.CredentialStoreUnavailable, error.Kind);
    }

    [Fact]
    public async Task HasKeyAsync_WhenTheStoreFails_ReportsNoKeyRatherThanThrowing()
    {
        _credentials.ThrowOnGet = true;

        Assert.False(await _store.HasKeyAsync(AiProviderKind.Claude));
    }
}
