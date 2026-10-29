using Gili.Core.Todos;
using Gili.Data;
using Microsoft.Data.Sqlite;

namespace Gili.Core.Tests.Data;

public sealed class TodoStoreTests : IDisposable
{
    private static readonly DateTimeOffset Created = new(2026, 10, 27, 5, 0, 0, TimeSpan.Zero);
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "gili-todos-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private static Todo Slides => new(0, "Prepare meeting slides", new DateOnly(2026, 10, 27), new TimeOnly(15, 0),
        new DateTimeOffset(2026, 10, 27, 8, 0, 0, TimeSpan.Zero), null, 0, Created);

    [Fact]
    public void Added_todo_survives_a_restart()
    {
        Todo added;
        using (var db = Database.Open(_folder)) added = new TodoStore(db).Add(Slides);
        SqliteConnection.ClearAllPools();
        using var reopened = Database.Open(_folder);
        Assert.True(added.Id > 0);
        Assert.Equal([added], new TodoStore(reopened).All());
    }

    [Fact]
    public void Update_changes_every_field()
    {
        using var db = Database.Open(_folder);
        var store = new TodoStore(db);
        var added = store.Add(Slides);
        var changed = added with { Title = "Send invoice", DueDate = null, DueTime = null, RemindAt = null, DoneAt = Created.AddHours(2), SortOrder = 7 };
        store.Update(changed);
        Assert.Equal([changed], store.All());
    }

    [Fact]
    public void Delete_removes_it()
    {
        using var db = Database.Open(_folder);
        var store = new TodoStore(db);
        store.Delete(store.Add(Slides).Id);
        Assert.Empty(store.All());
    }

    [Fact]
    public void New_todos_go_last()
    {
        using var db = Database.Open(_folder);
        var store = new TodoStore(db);
        var first = store.Add(Slides);
        var second = store.Add(Slides with { Title = "Second" });
        Assert.True(second.SortOrder > first.SortOrder);
        Assert.Equal(["Prepare meeting slides", "Second"], store.All().Select(t => t.Title));
    }

    [Fact]
    public void A_version_1_database_gains_due_time()
    {
        Directory.CreateDirectory(_folder);
        using (var raw = new SqliteConnection($"Data Source={Path.Combine(_folder, Database.FileName)}"))
        {
            raw.Open();
            using var command = raw.CreateCommand();
            command.CommandText =
                "CREATE TABLE schema_version (version INTEGER NOT NULL); INSERT INTO schema_version VALUES (1);" +
                "CREATE TABLE todos (id INTEGER PRIMARY KEY AUTOINCREMENT, title TEXT NOT NULL, due_date TEXT, remind_at TEXT, done_at TEXT, sort_order INTEGER NOT NULL, created_at TEXT NOT NULL);" +
                "INSERT INTO todos (title, due_date, sort_order, created_at) VALUES ('Old task', '2026-10-20', 0, '2026-10-20T01:00:00.0000000+00:00');";
            command.ExecuteNonQuery();
        }
        SqliteConnection.ClearAllPools();
        using var db = Database.Open(_folder);
        Assert.Equal(2, db.Version);
        var old = Assert.Single(new TodoStore(db).All());
        Assert.Equal(("Old task", new DateOnly(2026, 10, 20), (TimeOnly?)null), (old.Title, old.DueDate!.Value, old.DueTime));
    }

    [Fact]
    public void Todo_reorder_writes_every_place()
    {
        using var db = Database.Open(_folder);
        var store = new TodoStore(db);
        var a = store.Add(Slides);
        var b = store.Add(Slides);
        var c = store.Add(Slides);
        store.Reorder([c.Id, a.Id, b.Id]);
        var all = store.All();
        Assert.Equal(new[] { c.Id, a.Id, b.Id }, all.Select(todo => todo.Id));
        Assert.Equal(new[] { 0, 1, 2 }, all.Select(todo => todo.SortOrder));
    }
}
