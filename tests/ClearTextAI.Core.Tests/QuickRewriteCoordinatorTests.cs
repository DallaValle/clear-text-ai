using ClearTextAI.Core.Models;
using ClearTextAI.Core.Services.AI;
using ClearTextAI.Core.Services.Quick;
using ClearTextAI.Core.Tests.Fakes;
using Xunit;

namespace ClearTextAI.Core.Tests;

public class QuickRewriteCoordinatorTests
{
    private sealed class FakeSelection : ISelectionTextService
    {
        public bool IsSupported => true;

        public string? Selection { get; set; } = "this sentence are wrong";

        public string? Replaced { get; private set; }

        public int CaptureCount { get; private set; }

        public TaskCompletionSource? CaptureGate { get; set; }

        public Exception? CaptureFailure { get; set; }

        public async Task<string?> CaptureSelectionAsync(CancellationToken cancellationToken = default)
        {
            CaptureCount++;

            if (CaptureGate is not null)
            {
                await CaptureGate.Task.ConfigureAwait(false);
            }

            if (CaptureFailure is not null)
            {
                throw CaptureFailure;
            }

            return Selection;
        }

        public Task ReplaceSelectionAsync(string text, CancellationToken cancellationToken = default)
        {
            Replaced = text;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeOverlay : IQuickRewriteOverlay
    {
        public List<string> Events { get; } = [];

        public string? LastMessage { get; private set; }

        public bool LastWasError { get; private set; }

        public Task ShowBusyAsync(string message)
        {
            Events.Add($"busy:{message}");
            return Task.CompletedTask;
        }

        public Task ShowMessageAsync(string message, bool isError)
        {
            Events.Add($"message:{message}");
            LastMessage = message;
            LastWasError = isError;
            return Task.CompletedTask;
        }

        public Task HideAsync()
        {
            Events.Add("hide");
            return Task.CompletedTask;
        }
    }

    private sealed class StubRewrite : IRewriteService
    {
        public Exception? Failure { get; set; }

        public string? Received { get; private set; }

        public Task<RewriteResult> RewriteAsync(string text, CancellationToken cancellationToken = default) =>
            RewriteAsync(text, new RewriteOptions(), cancellationToken);

        public Task<RewriteResult> RewriteAsync(string text, RewriteOptions options, CancellationToken cancellationToken = default)
        {
            Received = text;

            if (Failure is not null)
            {
                throw Failure;
            }

            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(new RewriteResult
            {
                OriginalText = text,
                RewrittenText = "This sentence is correct.",
                Provider = AiProviderKind.Claude,
                Model = "claude-opus-5",
                Options = options,
            });
        }
    }

    private static (QuickRewriteCoordinator Coordinator, FakeSelection Selection, StubRewrite Rewrite, FakeOverlay Overlay) Create()
    {
        var selection = new FakeSelection();
        var rewrite = new StubRewrite();
        var overlay = new FakeOverlay();
        return (new QuickRewriteCoordinator(selection, rewrite, overlay), selection, rewrite, overlay);
    }

    [Fact]
    public async Task Run_ReplacesTheSelectionWithTheImprovedText()
    {
        var (coordinator, selection, rewrite, overlay) = Create();

        await coordinator.RunAsync();

        Assert.Equal("this sentence are wrong", rewrite.Received);
        Assert.Equal("This sentence is correct.", selection.Replaced);
        Assert.Equal(["busy:Improving...", "hide"], overlay.Events);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Run_WithNothingSelected_SaysSoAndCallsNoProvider(string? selected)
    {
        var (coordinator, selection, rewrite, overlay) = Create();
        selection.Selection = selected;

        await coordinator.RunAsync();

        Assert.Null(rewrite.Received);
        Assert.Null(selection.Replaced);
        Assert.Equal("Select some text first.", overlay.LastMessage);
        Assert.True(overlay.LastWasError);
    }

    [Fact]
    public async Task Run_WhenTheProviderFails_ShowsTheMessageAndLeavesTheTextAlone()
    {
        var (coordinator, selection, rewrite, overlay) = Create();
        rewrite.Failure = AiProviderException.For(AiErrorKind.MissingApiKey, "Claude");

        await coordinator.RunAsync();

        Assert.Null(selection.Replaced);
        Assert.True(overlay.LastWasError);
        Assert.Contains("No API key is configured", overlay.LastMessage);
    }

    [Fact]
    public async Task Run_WhenThePlatformIsUnavailable_ShowsItsMessage()
    {
        var (coordinator, selection, rewrite, overlay) = Create();
        selection.CaptureFailure = new QuickRewriteUnavailableException("Allow Accessibility access.");

        await coordinator.RunAsync();

        Assert.Null(rewrite.Received);
        Assert.True(overlay.LastWasError);
        Assert.Equal("Allow Accessibility access.", overlay.LastMessage);
    }

    [Fact]
    public async Task Run_NeverSurfacesAnUnexpectedException()
    {
        var (coordinator, selection, rewrite, overlay) = Create();
        rewrite.Failure = new InvalidOperationException("internal detail");

        await coordinator.RunAsync();

        Assert.Null(selection.Replaced);
        Assert.Equal("Something went wrong while improving the text.", overlay.LastMessage);
        Assert.DoesNotContain("internal detail", overlay.LastMessage);
    }

    [Fact]
    public async Task Run_WhenCancelled_JustHidesTheOverlay()
    {
        var (coordinator, selection, _, overlay) = Create();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await coordinator.RunAsync(cts.Token);

        Assert.Null(selection.Replaced);
        Assert.Contains("hide", overlay.Events);
        Assert.False(overlay.LastWasError);
    }

    [Fact]
    public async Task Run_IgnoresASecondPressWhileOneIsStillInFlight()
    {
        var (coordinator, selection, _, _) = Create();
        selection.CaptureGate = new TaskCompletionSource();

        var first = coordinator.RunAsync();
        Assert.True(coordinator.IsRunning);

        await coordinator.RunAsync();
        Assert.Equal(1, selection.CaptureCount);

        selection.CaptureGate.SetResult();
        await first;

        Assert.False(coordinator.IsRunning);
    }

    [Fact]
    public async Task Run_CanBeUsedAgainAfterAFailure()
    {
        var (coordinator, selection, rewrite, _) = Create();
        rewrite.Failure = AiProviderException.For(AiErrorKind.Network, "Claude");
        await coordinator.RunAsync();

        rewrite.Failure = null;
        await coordinator.RunAsync();

        Assert.Equal("This sentence is correct.", selection.Replaced);
    }
}
