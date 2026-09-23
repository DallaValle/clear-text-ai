namespace ClearTextAI.Core.Services.Quick;

/// <summary>The small floating indicator shown over other applications while a quick rewrite runs.</summary>
public interface IQuickRewriteOverlay
{
    Task ShowBusyAsync(string message);

    Task ShowMessageAsync(string message, bool isError);

    Task HideAsync();
}
