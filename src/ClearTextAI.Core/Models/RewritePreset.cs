namespace ClearTextAI.Core.Models;

public enum RewritePreset
{
    Natural = 0,
    Professional = 1,
    Friendly = 2,
    Concise = 3,
    GrammarOnly = 4,

    /// <summary>Set automatically as soon as the user edits any option by hand.</summary>
    Custom = 5,
}
