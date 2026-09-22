using ClearTextAI.Core.Models;
using ClearTextAI.Core.Services.History;
using ClearTextAI.Core.Services.Settings;

namespace ClearTextAI.Core.Tests.Fakes;

/// <summary>An in-memory settings service so provider tests do not touch the disk.</summary>
public sealed class InMemorySettingsService : ISettingsService
{
    public InMemorySettingsService(AppSettings? settings = null) =>
        Current = (settings ?? AppSettings.CreateDefault()).Normalize();

    public AppSettings Current { get; private set; }

    public event EventHandler? SettingsChanged;

    public int SaveCount { get; private set; }

    public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current);

    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        Current.Normalize();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryHistoryStore : IHistoryStore
{
    private readonly List<RewriteHistoryItem> _items = [];

    public IReadOnlyList<RewriteHistoryItem> Items => _items;

    public Task<IReadOnlyList<RewriteHistoryItem>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RewriteHistoryItem>>(_items);

    public Task AddAsync(RewriteHistoryItem item, CancellationToken cancellationToken = default)
    {
        _items.Insert(0, item);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _items.RemoveAll(i => i.Id == id);
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _items.Clear();
        return Task.CompletedTask;
    }
}
