using Gili.Data;
using Microsoft.Data.Sqlite;

namespace Gili.Core.Tests.Data;

public sealed class DatabaseTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "gili-db-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private static List<string> Column(SqliteConnection connection, string sql, int index = 0)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var values = new List<string>();
        while (reader.Read()) values.Add(reader.GetValue(index).ToString()!);
        return values;
    }

    [Fact]
    public void Database_syncs_every_commit()
    {
        using var db = Database.Open(_folder);
        Assert.Equal(["2"], Column(db.Connection, "PRAGMA synchronous"));
    }

    [Fact]
    public void Opening_creates_every_prd_table()
    {
        using var db = Database.Open(_folder);
        var tables = Column(db.Connection, "SELECT name FROM sqlite_master WHERE type = 'table'");
        Assert.Superset(
            new HashSet<string> { "schema_version", "notes", "todos", "alarms", "timers", "stopwatch", "claude_window", "shelf_items", "projects", "project_tasks", "claude_weekly" },
            tables.ToHashSet());
    }

    [Fact]
    public void Version_is_the_latest_after_the_first_open()
    {
        using var db = Database.Open(_folder);
        Assert.Equal(7, db.Version);
    }

    [Fact]
    public void Opening_twice_runs_no_step_again()
    {
        Database.Open(_folder).Dispose();
        using var db = Database.Open(_folder);
        Assert.Equal(7, db.Version);
        Assert.Equal(["7"], Column(db.Connection, "SELECT version FROM schema_version"));
    }

    [Fact]
    public void An_older_files_clipboard_history_is_dropped()
    {
        using (var older = Database.Open(_folder))
        {
            using var command = older.Connection.CreateCommand();
            command.CommandText = "CREATE TABLE clipboard_history (id INTEGER PRIMARY KEY, text TEXT NOT NULL); INSERT INTO clipboard_history (text) VALUES ('secret'); " +
                "DELETE FROM schema_version; INSERT INTO schema_version (version) VALUES (6)";
            command.ExecuteNonQuery();
        }
        using var db = Database.Open(_folder);
        Assert.Equal(7, db.Version);
        Assert.Empty(Column(db.Connection, "SELECT name FROM sqlite_master WHERE name = 'clipboard_history'"));
    }

    [Fact]
    public void Journal_is_wal()
    {
        using var db = Database.Open(_folder);
        Assert.Equal(["wal"], Column(db.Connection, "PRAGMA journal_mode"));
    }

    [Fact]
    public void Notes_columns_match_the_prd()
    {
        using var db = Database.Open(_folder);
        Assert.Equal(
            ["id", "body", "color", "text_size", "sort_order", "created_at", "updated_at", "float_x", "float_y", "float_w", "float_h"],
            Column(db.Connection, "PRAGMA table_info(notes)", 1));
    }

    [Fact]
    public void Database_survives_an_unclosed_connection()
    {
        var db = Database.Open(_folder);
        using (var insert = db.Connection.CreateCommand())
        {
            insert.CommandText = "INSERT INTO notes (body, sort_order, created_at, updated_at) VALUES ('kept', 0, '2026-10-27T00:00:00Z', '2026-10-27T00:00:00Z')";
            insert.ExecuteNonQuery();
        }
        // A power cut: the files as they are on disk now, without closing or checkpointing.
        var copy = Path.Combine(_folder, "copy");
        Directory.CreateDirectory(copy);
        foreach (var file in Directory.GetFiles(_folder, "gili.db*"))
        {
            using var source = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var target = File.Create(Path.Combine(copy, Path.GetFileName(file)));
            source.CopyTo(target);
        }
        using var reopened = Database.Open(copy);
        Assert.Equal(["kept"], Column(reopened.Connection, "SELECT body FROM notes"));
        db.Dispose();
    }
}
