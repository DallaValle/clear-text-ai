using ClearTextAI.Core.Services.AI;

namespace ClearTextAI.Core.Services.Quick;

/// <summary>
/// Drives one press of the global shortcut: read the selection, rewrite it, put it back.
/// Platform specific pieces sit behind the three interfaces it depends on.
/// </summary>
public sealed class QuickRewriteCoordinator
{
    private readonly ISelectionTextService _selection;
    private readonly IRewriteService _rewrite;
    private readonly IQuickRewriteOverlay _overlay;

    /// <summary>A second press while one is in flight is ignored rather than queued.</summary>
    private int _running;

    public QuickRewriteCoordinator(
        ISelectionTextService selection,
        IRewriteService rewrite,
        IQuickRewriteOverlay overlay)
    {
        _selection = selection;
        _rewrite = rewrite;
        _overlay = overlay;
    }

    public bool IsRunning => Volatile.Read(ref _running) == 1;

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) == 1)
        {
            return;
        }

        try
        {
            var original = await _selection.CaptureSelectionAsync(cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(original))
            {
                await _overlay.ShowMessageAsync("Select some text first.", isError: true).ConfigureAwait(false);
                return;
            }

            await _overlay.ShowBusyAsync("Improving...").ConfigureAwait(false);

            var result = await _rewrite.RewriteAsync(original, cancellationToken).ConfigureAwait(false);
            await _selection.ReplaceSelectionAsync(result.RewrittenText, cancellationToken).ConfigureAwait(false);

            await _overlay.HideAsync().ConfigureAwait(false);
        }
        catch (AiProviderException ex)
        {
            await _overlay.ShowMessageAsync(ex.Message, isError: true).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await _overlay.HideAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The overlay floats over other apps, so it must never surface a raw exception.
            await _overlay.ShowMessageAsync("Something went wrong while improving the text.", isError: true)
                .ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref _running, 0);
        }
    }
}
