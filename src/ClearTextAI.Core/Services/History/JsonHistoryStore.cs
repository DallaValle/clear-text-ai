using System.Text.Json;
using ClearTextAI.Core.Models;
using ClearTextAI.Core.Services.Platform;

namespace ClearTextAI.Core.Services.History;

/// <summary>A single JSON file in the user's local app data folder. Nothing is uploaded anywhere.</summary>
public sealed class JsonHistoryStore : IHistoryStore
{
    public const int DefaultMaxItems = 200;

    private readonly IAppPaths _paths;
    private readonly int _maxItems;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonHistoryStore(IAppPaths paths, int maxItems = DefaultMaxItems)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxItems, 1);
        _paths = paths;
        _maxItems = maxItems;
    }

    public async Task<IReadOnlyList<RewriteHistoryItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await TryReadAsync(cancellationToken).ConfigureAwait(false) ?? [];
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AddAsync(RewriteHistoryItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existing = await TryReadAsync(cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                // The file is there but unreadable right now. Dropping this entry beats
                // replacing everything the user already has with a single item.
                return;
            }

            var items = new List<RewriteHistoryItem>(existing);
            items.Insert(0, item);

            if (items.Count > _maxItems)
            {
                items.RemoveRange(_maxItems, items.Count - _maxItems);
            }

            await WriteAsync(items, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existing = await TryReadAsync(cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                return;
            }

            var items = new List<RewriteHistoryItem>(existing);
            if (items.RemoveAll(i => i.Id == id) > 0)
            {
                await WriteAsync(items, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WriteAsync([], cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Returns the stored items, an empty list when there is nothing usable to keep,
    /// or null when the file exists but could not be read and must not be overwritten.
    /// </summary>
    private async Task<IReadOnlyList<RewriteHistoryItem>?> TryReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_paths.HistoryFilePath))
        {
            return [];
        }

        string json;
        try
        {
            json = await AtomicFile.ReadAllTextAsync(_paths.HistoryFilePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<RewriteHistoryItem>>(json, JsonDefaults.Options) ?? [];
            return items
                .Where(i => i is not null)
                .OrderByDescending(i => i.CreatedAt)
                .ToList();
        }
        catch (JsonException)
        {
            // Corrupt beyond repair: start over rather than block every future write.
            return [];
        }
    }

    private Task WriteAsync(IReadOnlyList<RewriteHistoryItem> items, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(items, JsonDefaults.Options);
        return AtomicFile.WriteAllTextAsync(_paths.HistoryFilePath, json, cancellationToken);
    }
}
