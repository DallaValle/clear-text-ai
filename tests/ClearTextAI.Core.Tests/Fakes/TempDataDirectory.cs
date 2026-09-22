using ClearTextAI.Core.Services.Platform;

namespace ClearTextAI.Core.Tests.Fakes;

/// <summary>An isolated data folder per test, removed on dispose.</summary>
public sealed class TempDataDirectory : IDisposable
{
    public TempDataDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cleartextai-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
        Paths = new AppPaths(Path);
    }

    public string Path { get; }

    public IAppPaths Paths { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
            // A locked temp folder must not fail a test run.
        }
    }
}
