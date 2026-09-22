using ClearTextAI.Core.Models;
using ClearTextAI.Core.Services.AI;
using ClearTextAI.Core.Tests.Fakes;
using ClearTextAI.Core.ViewModels;
using Xunit;

namespace ClearTextAI.Core.Tests;

public class MainViewModelTests
{
    private sealed class StubRewriteService : IRewriteService
    {
        private readonly Func<string, RewriteResult> _result;

        public StubRewriteService(Func<string, RewriteResult>? result = null) =>
            _result = result ?? (text => Success(text, text.ToUpperInvariant()));

        public Exception? Failure { get; set; }

        public Task<RewriteResult> RewriteAsync(string text, CancellationToken cancellationToken = default) =>
            RewriteAsync(text, new RewriteOptions(), cancellationToken);

        public Task<RewriteResult> RewriteAsync(string text, RewriteOptions options, CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_result(text));
        }

        private static RewriteResult Success(string original, string rewritten) => new()
        {
            OriginalText = original,
            RewrittenText = rewritten,
            Provider = AiProviderKind.Claude,
            Model = "claude-opus-5",
            Options = new RewriteOptions(),
            Duration = TimeSpan.FromMilliseconds(400),
        };
    }

    private static (MainViewModel Vm, StubRewriteService Rewrite, FakeClipboardService Clipboard, FakeNavigationService Navigation, InMemorySettingsService Settings)
        Create()
    {
        var rewrite = new StubRewriteService();
        var settings = new InMemorySettingsService();
        var clipboard = new FakeClipboardService();
        var navigation = new FakeNavigationService();
        return (new MainViewModel(rewrite, settings, clipboard, navigation), rewrite, clipboard, navigation, settings);
    }

    [Fact]
    public void CountsLabel_TracksTheInput()
    {
        var (vm, _, _, _, _) = Create();

        vm.InputText = "hello brave new world";

        Assert.Equal(21, vm.CharacterCount);
        Assert.Equal(4, vm.WordCount);
        Assert.Equal("21 characters, 4 words", vm.CountsLabel);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("   ", 0)]
    [InlineData("one", 1)]
    [InlineData("  spaced   out  words ", 3)]
    [InlineData("line\nbreaks\tcount", 3)]
    public void WordCount_HandlesWhitespaceVariants(string text, int expected)
    {
        var (vm, _, _, _, _) = Create();

        vm.InputText = text;

        Assert.Equal(expected, vm.WordCount);
    }

    [Fact]
    public void ImproveText_IsDisabledUntilThereIsInput()
    {
        var (vm, _, _, _, _) = Create();

        Assert.False(vm.ImproveTextCommand.CanExecute(null));

        vm.InputText = "something";

        Assert.True(vm.ImproveTextCommand.CanExecute(null));
    }

    [Fact]
    public async Task ImproveText_ShowsTheResultAndAStatusLine()
    {
        var (vm, _, _, _, _) = Create();
        vm.InputText = "make this better";

        await vm.ImproveTextCommand.ExecuteAsync(null);

        Assert.Equal("MAKE THIS BETTER", vm.OutputText);
        Assert.True(vm.HasOutput);
        Assert.False(vm.HasError);
        Assert.Contains("claude-opus-5", vm.StatusMessage);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task ImproveText_ShowsTheHumanReadableMessageWhenTheProviderFails()
    {
        var (vm, rewrite, _, _, _) = Create();
        vm.InputText = "text";
        rewrite.Failure = AiProviderException.For(AiErrorKind.Network, "Claude");

        await vm.ImproveTextCommand.ExecuteAsync(null);

        Assert.Equal("Unable to connect to Claude. Check your internet connection and try again.", vm.ErrorMessage);
        Assert.True(vm.HasError);
        Assert.Empty(vm.OutputText);
    }

    [Fact]
    public async Task ImproveText_NeverSurfacesAnUnexpectedException()
    {
        var (vm, rewrite, _, _, _) = Create();
        vm.InputText = "text";
        rewrite.Failure = new InvalidOperationException("internal detail with sk-ant-secret");

        await vm.ImproveTextCommand.ExecuteAsync(null);

        Assert.DoesNotContain("sk-ant-secret", vm.ErrorMessage);
        Assert.Equal("Something went wrong while improving the text. Try again.", vm.ErrorMessage);
    }

    [Fact]
    public async Task ImproveText_ClearsThePreviousErrorOnTheNextRun()
    {
        var (vm, rewrite, _, _, _) = Create();
        vm.InputText = "text";
        rewrite.Failure = AiProviderException.For(AiErrorKind.RateLimited, "Claude");
        await vm.ImproveTextCommand.ExecuteAsync(null);

        rewrite.Failure = null;
        await vm.ImproveTextCommand.ExecuteAsync(null);

        Assert.False(vm.HasError);
        Assert.Equal("TEXT", vm.OutputText);
    }

    [Fact]
    public async Task CopyOutput_PutsTheResultOnTheClipboard()
    {
        var (vm, _, clipboard, _, _) = Create();
        vm.InputText = "text";
        await vm.ImproveTextCommand.ExecuteAsync(null);

        await vm.CopyOutputCommand.ExecuteAsync(null);

        Assert.Equal("TEXT", clipboard.LastText);
        Assert.Equal("Copied to clipboard.", vm.StatusMessage);
    }

    [Fact]
    public void CopyAndReplace_AreDisabledWithoutAResult()
    {
        var (vm, _, _, _, _) = Create();

        Assert.False(vm.CopyOutputCommand.CanExecute(null));
        Assert.False(vm.UseOutputAsInputCommand.CanExecute(null));
    }

    [Fact]
    public async Task UseOutputAsInput_MovesTheResultIntoTheEditor()
    {
        var (vm, _, _, _, _) = Create();
        vm.InputText = "text";
        await vm.ImproveTextCommand.ExecuteAsync(null);

        vm.UseOutputAsInputCommand.Execute(null);

        Assert.Equal("TEXT", vm.InputText);
        Assert.Empty(vm.OutputText);
    }

    [Fact]
    public async Task ClearInput_ResetsEverything()
    {
        var (vm, _, _, _, _) = Create();
        vm.InputText = "text";
        await vm.ImproveTextCommand.ExecuteAsync(null);

        vm.ClearInputCommand.Execute(null);

        Assert.Empty(vm.InputText);
        Assert.Empty(vm.OutputText);
        Assert.False(vm.HasError);
        Assert.False(vm.HasStatus);
    }

    [Fact]
    public void SelectingAPreset_AppliesAndPersistsIt()
    {
        var (vm, _, _, _, settings) = Create();

        vm.SelectedPreset = vm.Presets.First(p => p.Value == RewritePreset.Concise);

        Assert.Equal(RewritePreset.Concise, settings.Current.RewriteOptions.Preset);
        Assert.Equal(RewriteTone.Concise, settings.Current.RewriteOptions.Tone);
        Assert.Equal(1, settings.SaveCount);
    }

    [Fact]
    public async Task Navigation_UsesTheSharedRoutes()
    {
        var (vm, _, _, navigation, _) = Create();

        await vm.OpenSettingsCommand.ExecuteAsync(null);
        await vm.OpenHistoryCommand.ExecuteAsync(null);

        Assert.Equal(["settings", "history"], navigation.Routes);
    }

    [Fact]
    public async Task PrivacyNotice_NamesTheActiveProvider()
    {
        var (vm, _, _, _, settings) = Create();

        Assert.Contains("Claude", vm.PrivacyNotice);

        settings.Current.ActiveProvider = AiProviderKind.OpenAi;
        await settings.SaveAsync();

        Assert.Contains("OpenAI", vm.PrivacyNotice);
    }
}
