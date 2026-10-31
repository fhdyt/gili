using Gili.Core.Shelf;

namespace Gili.Core.Tests.Shelf;

public class ShelfServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);

    private readonly Dictionary<string, FileFacts> _disk = new()
    {
        [@"C:\a.pdf"] = new(false, 2_000),
        [@"C:\b.png"] = new(false, 3_000),
        [@"C:\c.zip"] = new(false, 5_000),
        [@"C:\Photos"] = new(true, 0),
    };

    private readonly FakeStore _store = new();

    private ShelfService Create(IShelfStore? store = null) => new(store ?? _store, p => _disk.TryGetValue(p, out var f) ? f : null);

    [Fact]
    public void One_file_is_one_item_named_after_it()
    {
        var shelf = Create();
        var item = shelf.Add([@"C:\a.pdf"], Now)!;
        Assert.Equal("a.pdf", item.DisplayName);
        Assert.Equal(2_000, item.SizeBytes);
        Assert.False(item.IsBundle);
        Assert.Single(shelf.Items);
    }

    [Fact]
    public void Several_files_are_a_bundle_named_by_count()
    {
        var shelf = Create();
        var item = shelf.Add([@"C:\a.pdf", @"C:\b.png", @"C:\c.zip"], Now)!;
        Assert.Equal("3 files", item.DisplayName);
        Assert.True(item.IsBundle);
        Assert.Equal(10_000, shelf.TotalBytes);
        Assert.Equal(3, shelf.FileCount);
    }

    [Fact]
    public void Duplicate_paths_in_one_drop_count_once() =>
        Assert.Equal([@"C:\a.pdf"], Create().Add([@"C:\a.pdf", @"C:\a.pdf"], Now)!.Paths);

    [Fact]
    public void Missing_paths_are_not_added()
    {
        var shelf = Create();
        Assert.Null(shelf.Add([@"C:\gone.txt"], Now));
        Assert.Equal([@"C:\a.pdf"], shelf.Add([@"C:\gone.txt", @"C:\a.pdf"], Now)!.Paths);
    }

    [Fact]
    public void Full_shelf_refuses_then_accepts_after_remove()
    {
        var shelf = Create();
        for (var i = 0; i < ShelfService.MaxItems; i++) shelf.Add([@"C:\a.pdf"], Now);
        Assert.True(shelf.IsFull);
        Assert.Null(shelf.Add([@"C:\b.png"], Now));
        shelf.Remove(shelf.Items[0].Id);
        Assert.False(shelf.IsFull);
        Assert.NotNull(shelf.Add([@"C:\b.png"], Now));
        Assert.Equal(8, shelf.Items.Count);
    }

    [Fact]
    public void Prune_removes_gone_items()
    {
        var shelf = Create();
        shelf.Add([@"C:\a.pdf"], Now);
        shelf.Add([@"C:\b.png"], Now);
        _disk.Remove(@"C:\a.pdf");
        Assert.True(shelf.Prune());
        Assert.Equal(["b.png"], shelf.Items.Select(i => i.DisplayName));
        Assert.Single(_store.Items);
        Assert.False(shelf.Prune());
    }

    [Fact]
    public void Bundle_drops_missing_files_and_renames()
    {
        var shelf = Create();
        var bundle = shelf.Add([@"C:\a.pdf", @"C:\b.png", @"C:\c.zip"], Now)!;
        _disk.Remove(@"C:\a.pdf");
        shelf.PruneItem(bundle.Id);
        Assert.Equal("2 files", shelf.Items[0].DisplayName);
        Assert.Equal(8_000, shelf.Items[0].SizeBytes);
        _disk.Remove(@"C:\b.png");
        shelf.Prune();
        Assert.Equal("c.zip", shelf.Items[0].DisplayName);
        Assert.Equal([@"C:\c.zip"], _store.Items.Single().Paths);
        _disk.Remove(@"C:\c.zip");
        shelf.Prune();
        Assert.Empty(shelf.Items);
    }

    [Fact]
    public void Clear_empties_and_saves()
    {
        var shelf = Create();
        shelf.Add([@"C:\a.pdf"], Now);
        var changed = 0;
        shelf.Changed += () => changed++;
        shelf.Clear();
        Assert.Empty(shelf.Items);
        Assert.Empty(_store.Items);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void Items_survive_a_new_service()
    {
        Create().Add([@"C:\b.png"], Now);
        Assert.Equal(["b.png"], Create().Items.Select(i => i.DisplayName));
    }

    [Fact]
    public void Works_without_a_store()
    {
        var shelf = new ShelfService(null, p => _disk.TryGetValue(p, out var f) ? f : null);
        var item = shelf.Add([@"C:\a.pdf"], Now)!;
        Assert.Single(shelf.Items);
        shelf.Remove(item.Id);
        Assert.Empty(shelf.Items);
    }

    [Fact]
    public void Demo_item_shows_first_and_is_never_saved()
    {
        var shelf = Create();
        shelf.Add([@"C:\b.png"], Now);
        var demo = new ShelfItem(-1, [@"C:\Demo\Quarterly report.pdf"], "Quarterly report.pdf", 2_400_000, 0, Now);
        shelf.ShowDemo(demo);
        Assert.Equal(["Quarterly report.pdf", "b.png"], shelf.Items.Select(i => i.DisplayName));
        Assert.Equal(2, shelf.FileCount);
        Assert.False(shelf.Prune());
        Assert.Single(_store.Items);
        shelf.ShowDemo(null);
        Assert.Equal(["b.png"], shelf.Items.Select(i => i.DisplayName));
    }

    [Fact]
    public void Folder_counts_as_one_file_of_size_zero()
    {
        var shelf = Create();
        var item = shelf.Add([@"C:\Photos"], Now)!;
        Assert.Equal("Photos", item.DisplayName);
        Assert.Equal(0, item.SizeBytes);
        Assert.Equal(1, shelf.FileCount);
    }

    private sealed class FakeStore : IShelfStore
    {
        private long _next = 1;

        public List<ShelfItem> Items { get; } = [];

        public IReadOnlyList<ShelfItem> All() => [.. Items];

        public ShelfItem Add(ShelfItem item)
        {
            var added = item with { Id = _next++ };
            Items.Add(added);
            return added;
        }

        public void Update(ShelfItem item) => Items[Items.FindIndex(i => i.Id == item.Id)] = item;

        public void Delete(long id) => Items.RemoveAll(i => i.Id == id);

        public void Clear() => Items.Clear();
    }
}
