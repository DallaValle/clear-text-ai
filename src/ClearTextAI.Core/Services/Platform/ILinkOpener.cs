namespace ClearTextAI.Core.Services.Platform;

/// <summary>Opens a URL in the user's browser.</summary>
public interface ILinkOpener
{
    Task OpenAsync(string url);
}
