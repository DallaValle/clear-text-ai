namespace ClearTextAI.Core.Models;

/// <summary>One entry in the local rewrite history. Never leaves the machine.</summary>
public sealed class RewriteHistoryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public string OriginalText { get; set; } = string.Empty;

    public string RewrittenText { get; set; } = string.Empty;

    public AiProviderKind Provider { get; set; }

    public string Model { get; set; } = string.Empty;

    public RewriteOptions Options { get; set; } = new();

    public static RewriteHistoryItem FromResult(RewriteResult result) => new()
    {
        Id = Guid.NewGuid(),
        CreatedAt = result.CompletedAt,
        OriginalText = result.OriginalText,
        RewrittenText = result.RewrittenText,
        Provider = result.Provider,
        Model = result.Model,
        Options = result.Options.Clone(),
    };
}
