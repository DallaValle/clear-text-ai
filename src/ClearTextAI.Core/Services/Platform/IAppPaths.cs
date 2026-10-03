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

    /// <summary>%LOCALAPPDATA%\ClearTextAI on Windows.</summary>
    public static AppPaths CreateDefault() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
        FolderName));

    /// <summary>
    /// Copies the data files from a previous location when this one has none yet, so moving the
    /// data folder keeps the user's settings and history. Existing files are never overwritten.
    /// </summary>
    public void ImportMissingFrom(string previousDirectory)
    {
        if (!Directory.Exists(previousDirectory)
            || Path.GetFullPath(previousDirectory) == Path.GetFullPath(DataDirectory))
        {
            return;
        }

        Directory.CreateDirectory(DataDirectory);

        foreach (var target in new[] { SettingsFilePath, HistoryFilePath })
        {
            var source = Path.Combine(previousDirectory, Path.GetFileName(target));
            if (File.Exists(source) && !File.Exists(target))
            {
                File.Copy(source, target);
            }
        }
    }
}
