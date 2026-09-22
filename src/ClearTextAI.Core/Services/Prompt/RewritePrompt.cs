namespace ClearTextAI.Core.Services.Prompt;

/// <summary>A provider-agnostic prompt pair. Providers map it onto their own request shape.</summary>
/// <param name="System">Instructions and constraints.</param>
/// <param name="User">The delimited text to rewrite.</param>
public sealed record RewritePrompt(string System, string User);
