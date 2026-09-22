namespace ClearTextAI.Core.Services.Platform;

public interface IClipboardService
{
    Task SetTextAsync(string text);
}
