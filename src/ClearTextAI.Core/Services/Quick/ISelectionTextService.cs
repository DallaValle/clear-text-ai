namespace ClearTextAI.Core.Services.Quick;

/// <summary>Reads and replaces the selected text in whatever application currently has focus.</summary>
public interface ISelectionTextService
{
    bool IsSupported { get; }

    /// <summary>Null when nothing is selected or the foreground app did not answer in time.</summary>
    Task<string?> CaptureSelectionAsync(CancellationToken cancellationToken = default);

    Task ReplaceSelectionAsync(string text, CancellationToken cancellationToken = default);
}
