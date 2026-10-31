using Gili.Core.Notes;
using Gili.Data;
using Microsoft.Data.Sqlite;

namespace Gili.Core.Tests.Data;

public sealed class NoteStoreTests : IDisposable
{
    private static readonly DateTimeOffset Created = new(2026, 10, 27, 5, 0, 0, TimeSpan.Zero);
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "gili-notes-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private static Note Make(string body, int color = 0, NoteTextSize size = NoteTextSize.Medium) =>
        new(0, body, color, size, 0, Created, Created.AddMinutes(3));

    [Fact]
    public void Added_note_goes_first_and_survives_a_restart()
    {
        Note first, second;
        using (var db = Database.Open(_folder))
        {
            var store = new NoteStore(db);
            first = store.Add(Make("First", 3, NoteTextSize.Large));
            second = store.Add(Make("Second", 5, NoteTextSize.ExtraSmall));
            Assert.True(second.SortOrder < first.SortOrder);
        }
        SqliteConnection.ClearAllPools();
        using var reopened = Database.Open(_folder);
        Assert.Equal([second, first], new NoteStore(reopened).All());
    }

    [Fact]
    public void Update_keeps_order_and_float_columns()
    {
        using var db = Database.Open(_folder);
        var store = new NoteStore(db);
        var added = store.Add(Make("Hello"));
        using (var command = db.Connection.CreateCommand())
        {
            command.CommandText = "UPDATE notes SET float_x = 120 WHERE id = $id";
            command.Parameters.AddWithValue("$id", added.Id);
            command.ExecuteNonQuery();
        }
        var changed = added with { Body = "Changed", Color = 2, Size = NoteTextSize.Small, UpdatedAt = Created.AddHours(1), SortOrder = 99 };
        store.Update(changed);
        Assert.Equal([changed with { SortOrder = added.SortOrder }], store.All());
        using var check = db.Connection.CreateCommand();
        check.CommandText = "SELECT float_x, sort_order FROM notes";
        using var reader = check.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(120, reader.GetDouble(0));
        Assert.Equal(added.SortOrder, reader.GetInt32(1));
    }

    [Fact]
    public void Reorder_writes_the_new_order()
    {
        using var db = Database.Open(_folder);
        var store = new NoteStore(db);
        var a = store.Add(Make("a"));
        var b = store.Add(Make("b"));
        var c = store.Add(Make("c"));
        store.Reorder([a.Id, c.Id, b.Id]);
        var all = store.All();
        Assert.Equal(new[] { a.Id, c.Id, b.Id }, all.Select(note => note.Id));
        Assert.Equal(new[] { 0, 1, 2 }, all.Select(note => note.SortOrder));
    }

    [Fact]
    public void A_floating_window_survives_a_restart_and_clears()
    {
        long id;
        var window = new NoteRect(1200.5, 64, 300, 260);
        using (var db = Database.Open(_folder))
        {
            var store = new NoteStore(db);
            id = store.Add(Make("Float me")).Id;
            store.SetFloat(id, window);
        }
        SqliteConnection.ClearAllPools();
        using var reopened = Database.Open(_folder);
        var again = new NoteStore(reopened);
        Assert.Equal(window, Assert.Single(again.All()).Float);
        again.SetFloat(id, null);
        Assert.Null(Assert.Single(again.All()).Float);
    }

    [Fact]
    public void Delete_removes_it()
    {
        using var db = Database.Open(_folder);
        var store = new NoteStore(db);
        store.Delete(store.Add(Make("x")).Id);
        Assert.Empty(store.All());
    }

    [Fact]
    public void Blank_notes_are_dropped_on_start()
    {
        using var db = Database.Open(_folder);
        var store = new NoteStore(db);
        store.Add(Make(""));
        store.Add(Make(" \n "));
        var kept = store.Add(Make("a"));
        Assert.Equal(2, store.DeleteBlank());
        Assert.Equal(new[] { kept.Id }, store.All().Select(note => note.Id));
    }
}
