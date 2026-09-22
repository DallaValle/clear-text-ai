namespace ClearTextAI.Core.Models;

/// <summary>The outcome of one successful rewrite, including what produced it.</summary>
public sealed record RewriteResult
{
    public required string OriginalText { get; init; }

    public required string RewrittenText { get; init; }

    public required AiProviderKind Provider { get; init; }

    public required string Model { get; init; }

    public required RewriteOptions Options { get; init; }

    public TimeSpan Duration { get; init; }

    public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.Now;
}
