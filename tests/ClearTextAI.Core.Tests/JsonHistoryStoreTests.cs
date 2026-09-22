using ClearTextAI.Core.Models;
using ClearTextAI.Core.Services.History;
using ClearTextAI.Core.Tests.Fakes;
using Xunit;

namespace ClearTextAI.Core.Tests;

public class JsonHistoryStoreTests : IDisposable
{
    private readonly TempDataDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    private JsonHistoryStore CreateStore(int maxItems = JsonHistoryStore.DefaultMaxItems) =>
        new(_directory.Paths, maxItems);

    private static RewriteHistoryItem CreateItem(string original = "before", string rewritten = "after") => new()
    {
        OriginalText = original,
        RewrittenText = rewritten,
        Provider = AiProviderKind.Claude,
        Model = "claude-opus-5",
        Options = RewritePresets.Create(RewritePreset.Friendly),
    };

    [Fact]
    public async Task GetAllAsync_WithNoFile_ReturnsEmpty()
    {
        Assert.Empty(await CreateStore().GetAllAsync());
    }

    [Fact]
    public async Task AddAsync_RoundTripsEverySavedField()
    {
        var store = CreateStore();
        var item = CreateItem("i has bad grammar", "My grammar is bad.");

        await store.AddAsync(item);

        var stored = Assert.Single(await CreateStore().GetAllAsync());
        Assert.Equal(item.Id, stored.Id);
        Assert.Equal("i has bad grammar", stored.OriginalText);
        Assert.Equal("My grammar is bad.", stored.RewrittenText);
        Assert.Equal(AiProviderKind.Claude, stored.Provider);
        Assert.Equal("claude-opus-5", stored.Model);
        Assert.Equal(RewriteTone.Friendly, stored.Options.Tone);
        Assert.Equal(item.CreatedAt.ToUnixTimeSeconds(), stored.CreatedAt.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task GetAllAsync_ReturnsNewestFirst()
    {
        var store = CreateStore();
        var older = CreateItem("old");
        older.CreatedAt = DateTimeOffset.Now.AddHours(-2);
        var newer = CreateItem("new");
        newer.CreatedAt = DateTimeOffset.Now;

        await store.AddAsync(older);
        await store.AddAsync(newer);

        var items = await store.GetAllAsync();
        Assert.Equal("new", items[0].OriginalText);
        Assert.Equal("old", items[1].OriginalText);
    }

    [Fact]
    public async Task AddAsync_KeepsOnlyTheMostRecentItemsWithinTheCap()
    {
        var store = CreateStore(maxItems: 3);

        for (var i = 0; i < 6; i++)
        {
            var item = CreateItem($"text {i}");
            item.CreatedAt = DateTimeOffset.Now.AddMinutes(i);
            await store.AddAsync(item);
        }

        var items = await store.GetAllAsync();
        Assert.Equal(3, items.Count);
        Assert.Equal("text 5", items[0].OriginalText);
        Assert.Equal("text 3", items[2].OriginalText);
    }

    [Fact]
    public async Task DeleteAsync_RemovesOnlyTheRequestedEntry()
    {
        var store = CreateStore();
        var keep = CreateItem("keep");
        var drop = CreateItem("drop");
        await store.AddAsync(keep);
        await store.AddAsync(drop);

        await store.DeleteAsync(drop.Id);

        var remaining = Assert.Single(await store.GetAllAsync());
        Assert.Equal(keep.Id, remaining.Id);
    }

    [Fact]
    public async Task DeleteAsync_WithUnknownId_IsANoOp()
    {
        var store = CreateStore();
        await store.AddAsync(CreateItem());

        await store.DeleteAsync(Guid.NewGuid());

        Assert.Single(await store.GetAllAsync());
    }

    [Fact]
    public async Task ClearAsync_RemovesEverything()
    {
        var store = CreateStore();
        await store.AddAsync(CreateItem());
        await store.AddAsync(CreateItem());

        await store.ClearAsync();

        Assert.Empty(await store.GetAllAsync());
        Assert.Empty(await CreateStore().GetAllAsync());
    }

    [Fact]
    public async Task GetAllAsync_WithACorruptFile_DegradesToEmptyInsteadOfThrowing()
    {
        await File.WriteAllTextAsync(_directory.Paths.HistoryFilePath, "{ this is not json");

        Assert.Empty(await CreateStore().GetAllAsync());
    }

    [Fact]
    public async Task AddAsync_RecoversFromACorruptFile()
    {
        await File.WriteAllTextAsync(_directory.Paths.HistoryFilePath, "not json at all");
        var store = CreateStore();

        await store.AddAsync(CreateItem("fresh"));

        var item = Assert.Single(await store.GetAllAsync());
        Assert.Equal("fresh", item.OriginalText);
    }

    [Fact]
    public async Task ConcurrentWrites_AllSurvive()
    {
        var store = CreateStore();

        await Task.WhenAll(Enumerable.Range(0, 20).Select(i => store.AddAsync(CreateItem($"text {i}"))));

        Assert.Equal(20, (await store.GetAllAsync()).Count);
    }

    [Fact]
    public async Task HistoryIsStoredInTheUsersLocalDataFolderOnly()
    {
        await CreateStore().AddAsync(CreateItem());

        Assert.True(File.Exists(_directory.Paths.HistoryFilePath));
        Assert.StartsWith(_directory.Path, _directory.Paths.HistoryFilePath);
    }

    [Fact]
    public void Constructor_RejectsANonPositiveCap()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new JsonHistoryStore(_directory.Paths, 0));
    }
}
