namespace ClearTextAI.Core.Services.Platform;

/// <summary>Where the app keeps its local files. Everything stays on the user's machine.</summary>
public interface IAppPaths
{
    string DataDirectory { get; }

    string SettingsFilePath { get; }

    string HistoryFilePath { get; }
}

public sealed class AppPaths : IAppPaths
{
    public const string FolderName = "ClearTextAI";

    public AppPaths(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        DataDirectory = dataDirectory;
    }

    public string DataDirectory { get; }

    public string SettingsFilePath => Path.Combine(DataDirectory, "settings.json");

    public string HistoryFilePath => Path.Combine(DataDirectory, "history.json");

    /// <summary>
    /// %LOCALAPPDATA%\ClearTextAI on Windows, ~/Library/Application Support/ClearTextAI inside
    /// the app container on macOS.
    /// </summary>
    public static AppPaths CreateDefault() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
        FolderName));
}
