using ClearTextAI.Core.Models;

namespace ClearTextAI.Core.Services.Prompt;

/// <summary>Turns rewrite options into the prompt sent to a provider. Kept out of the UI and out of the providers.</summary>
public interface IPromptBuilder
{
    RewritePrompt Build(string text, RewriteOptions options);
}
