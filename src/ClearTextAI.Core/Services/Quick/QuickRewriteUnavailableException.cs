namespace ClearTextAI.Core.Services.Quick;

/// <summary>The platform cannot run a quick rewrite right now. The message is safe to show as is.</summary>
public sealed class QuickRewriteUnavailableException : Exception
{
    public QuickRewriteUnavailableException(string message)
        : base(message)
    {
    }
}
