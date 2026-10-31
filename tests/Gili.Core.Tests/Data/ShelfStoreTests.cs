using Gili.Core.Shelf;
using Gili.Data;
using Microsoft.Data.Sqlite;

namespace Gili.Core.Tests.Data;

public sealed class ShelfStoreTests : IDisposable
{
    private static readonly DateTimeOffset Added = new(2026, 10, 31, 2, 0, 0, TimeSpan.Zero);
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "gili-shelf-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void Add_load_update_delete_and_clear()
    {
        ShelfItem bundle;
        using (var db = Database.Open(_folder))
        {
            var store = new ShelfStore(db);
            store.Add(new ShelfItem(0, [@"C:\Users\me\Downloads\a.pdf"], "a.pdf", 2_000, 0, Added));
            bundle = store.Add(new ShelfItem(0, [@"C:\x y\b.png", @"D:\ünï\c.zip"], "2 files", 8_000, 1, Added));
        }
        using (var db = Database.Open(_folder))
        {
            var store = new ShelfStore(db);
            var all = store.All();
            Assert.Equal(["a.pdf", "2 files"], all.Select(i => i.DisplayName));
            Assert.Equal([@"C:\x y\b.png", @"D:\ünï\c.zip"], all[1].Paths);
            Assert.Equal(Added, all[1].AddedAt);
            Assert.Equal(bundle.Id, all[1].Id);

            store.Update(all[1] with { Paths = [@"D:\ünï\c.zip"], DisplayName = "c.zip", SizeBytes = 5_000 });
            Assert.Equal(("c.zip", 5_000L), (store.All()[1].DisplayName, store.All()[1].SizeBytes));

            store.Delete(all[0].Id);
            Assert.Single(store.All());
            store.Clear();
            Assert.Empty(store.All());
        }
    }
}
