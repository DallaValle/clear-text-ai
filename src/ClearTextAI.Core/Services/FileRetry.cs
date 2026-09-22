namespace ClearTextAI.Core.Services;

/// <summary>
/// Local files can be briefly locked by a virus scanner, a backup agent or an indexer.
/// A short retry turns those transient failures back into success instead of data loss.
/// </summary>
internal static class FileRetry
{
    private const int MaxAttempts = 4;
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(40);

    public static async Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await action().ConfigureAwait(false);
            }
            catch (IOException) when (attempt < MaxAttempts)
            {
                await Task.Delay(Delay, cancellationToken).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException) when (attempt < MaxAttempts)
            {
                await Task.Delay(Delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public static Task RunAsync(Func<Task> action, CancellationToken cancellationToken) =>
        RunAsync<object?>(async () =>
        {
            await action().ConfigureAwait(false);
            return null;
        }, cancellationToken);
}
