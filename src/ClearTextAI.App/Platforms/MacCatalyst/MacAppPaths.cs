using ClearTextAI.Core.Services.Platform;

namespace ClearTextAI.App.Platforms.MacCatalyst;

/// <summary>
/// ~/Library/Application Support/ClearTextAI. Outside the sandbox LocalApplicationData resolves to
/// ~/Documents, which is no place for app data, so the folder is spelled out.
/// </summary>
public static class MacAppPaths
{
    private const string BundleId = "com.cleartextai.app";

    public static AppPaths Create()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var paths = new AppPaths(Path.Combine(home, "Library", "Application Support", AppPaths.FolderName));

        try
        {
            // Earlier builds were sandboxed and kept their data inside the app container.
            paths.ImportMissingFrom(Path.Combine(home, "Library", "Containers", BundleId, "Data", "Documents", AppPaths.FolderName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A failed import only means starting with default settings.
        }

        return paths;
    }
}
