using ClearTextAI.Core.Services.Platform;
using ClearTextAI.Core.Tests.Fakes;
using Xunit;

namespace ClearTextAI.Core.Tests;

public class AppPathsTests
{
    [Fact]
    public void ImportMissingFrom_CopiesSettingsAndHistory()
    {
        using var previous = new TempDataDirectory();
        using var current = new TempDataDirectory();
        File.WriteAllText(previous.Paths.SettingsFilePath, "old settings");
        File.WriteAllText(previous.Paths.HistoryFilePath, "old history");

        ((AppPaths)current.Paths).ImportMissingFrom(previous.Path);

        Assert.Equal("old settings", File.ReadAllText(current.Paths.SettingsFilePath));
        Assert.Equal("old history", File.ReadAllText(current.Paths.HistoryFilePath));
    }

    [Fact]
    public void ImportMissingFrom_NeverOverwritesExistingFiles()
    {
        using var previous = new TempDataDirectory();
        using var current = new TempDataDirectory();
        File.WriteAllText(previous.Paths.SettingsFilePath, "old settings");
        File.WriteAllText(current.Paths.SettingsFilePath, "new settings");

        ((AppPaths)current.Paths).ImportMissingFrom(previous.Path);

        Assert.Equal("new settings", File.ReadAllText(current.Paths.SettingsFilePath));
    }

    [Fact]
    public void ImportMissingFrom_IgnoresAMissingFolder()
    {
        using var current = new TempDataDirectory();

        ((AppPaths)current.Paths).ImportMissingFrom(Path.Combine(current.Path, "does-not-exist"));

        Assert.False(File.Exists(current.Paths.SettingsFilePath));
    }
}
