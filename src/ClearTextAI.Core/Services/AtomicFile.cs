namespace ClearTextAI.Core.Services;

internal static class AtomicFile
{
    /// <summary>Writes through a temp file so a crash mid-write cannot leave a half-written settings or history file.</summary>
    public static Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + ".tmp";

        return FileRetry.RunAsync(async () =>
        {
            await File.WriteAllTextAsync(tempPath, contents, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, path, overwrite: true);
        }, cancellationToken);
    }

    public static Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) =>
        FileRetry.RunAsync(() => File.ReadAllTextAsync(path, cancellationToken), cancellationToken);
}
