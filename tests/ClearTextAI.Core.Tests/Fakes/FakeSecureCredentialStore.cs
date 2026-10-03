using System.Collections.Concurrent;
using ClearTextAI.Core.Services.SecureStorage;

namespace ClearTextAI.Core.Tests.Fakes;

public sealed class FakeSecureCredentialStore : ISecureCredentialStore
{
    private readonly ConcurrentDictionary<string, string> _values = new();

    public bool ThrowOnGet { get; set; }

    public bool ThrowOnWrite { get; set; }

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (ThrowOnGet)
        {
            throw new InvalidOperationException("credential store offline");
        }

        return Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);
    }

    public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        if (ThrowOnWrite)
        {
            throw new InvalidOperationException("credential store offline");
        }

        _values[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _values.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
