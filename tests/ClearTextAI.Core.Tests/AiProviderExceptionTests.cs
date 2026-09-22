using ClearTextAI.Core.Services.AI;
using Xunit;

namespace ClearTextAI.Core.Tests;

public class AiProviderExceptionTests
{
    [Fact]
    public void EveryErrorKindHasANonTechnicalMessage()
    {
        foreach (var kind in Enum.GetValues<AiErrorKind>())
        {
            var error = AiProviderException.For(kind, "Claude");

            Assert.False(string.IsNullOrWhiteSpace(error.Message));
            Assert.EndsWith(".", error.Message);

            foreach (var jargon in new[] { "Exception", "status code", "stack", "null", "System." })
            {
                Assert.DoesNotContain(jargon, error.Message, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void NetworkMessage_TellsTheUserWhatToDo()
    {
        var error = AiProviderException.For(AiErrorKind.Network, "Claude");

        Assert.Equal("Unable to connect to Claude. Check your internet connection and try again.", error.Message);
    }

    [Theory]
    [InlineData(AiErrorKind.RateLimited)]
    [InlineData(AiErrorKind.Timeout)]
    [InlineData(AiErrorKind.Network)]
    [InlineData(AiErrorKind.ProviderUnavailable)]
    public void TransientFailures_AreMarkedAsRetryable(AiErrorKind kind)
    {
        Assert.True(AiProviderException.For(kind, "Claude").IsTransient);
    }

    [Theory]
    [InlineData(AiErrorKind.MissingApiKey)]
    [InlineData(AiErrorKind.InvalidApiKey)]
    [InlineData(AiErrorKind.EmptyInput)]
    [InlineData(AiErrorKind.InvalidRequest)]
    public void PermanentFailures_AreNotMarkedAsRetryable(AiErrorKind kind)
    {
        Assert.False(AiProviderException.For(kind, "Claude").IsTransient);
    }

    [Fact]
    public void InnerExceptionDetailsNeverReachTheMessage()
    {
        var inner = new InvalidOperationException("x-api-key sk-ant-secret rejected for request body 'hello'");

        var error = AiProviderException.For(AiErrorKind.InvalidApiKey, "Claude", inner);

        Assert.DoesNotContain("sk-ant-secret", error.Message);
        Assert.Same(inner, error.InnerException);
    }
}
