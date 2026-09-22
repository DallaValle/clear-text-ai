namespace ClearTextAI.Core.Models;

public enum RewriteTone
{
    /// <summary>Default: keep whatever tone the author already used.</summary>
    PreserveOriginal = 0,
    Friendly = 1,
    Professional = 2,
    Casual = 3,
    Formal = 4,
    Concise = 5,
}
