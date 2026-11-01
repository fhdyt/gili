using Gili.Data;
using Microsoft.Data.Sqlite;

namespace Gili.Core.Tests.Data;

/// <summary>Copies outside the data folder bring the notes back after a reinstall (design 2026-10-31 data tetap).</summary>
public sealed class KeepCopiesTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 31);
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gili-keep-" + Guid.NewGuid().ToString("N"));

    private string OldData => Path.Combine(_root, "old");
    private string NewData => Path.Combine(_root, "new");
    private string Keep => Path.Combine(_root, "keep");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static void AddNote(Database db, string body)
    {
        using var insert = db.Connection.CreateCommand();
        insert.CommandText = "INSERT INTO notes (body, sort_order, created_at, updated_at) VALUES ($b, 0, 'x', 'x')";
        insert.Parameters.AddWithValue("$b", body);
        insert.ExecuteNonQuery();
    }

    private static List<string> Notes(Database db)
    {
        using var select = db.Connection.CreateCommand();
        select.CommandText = "SELECT body FROM notes ORDER BY id";
        using var reader = select.ExecuteReader();
        var bodies = new List<string>();
        while (reader.Read()) bodies.Add(reader.GetString(0));
        return bodies;
    }

    [Fact]
    public void A_new_install_opens_the_latest_keep_copy()
    {
        using (var old = Database.Open(OldData, Keep))
        {
            AddNote(old, "groceries");
            Backups.Refresh(old, Keep, Today.AddDays(-1));
            AddNote(old, "call mum");
            Backups.Refresh(old, Keep, Today);
        }

        using var fresh = Database.Open(NewData, Keep);
        Assert.Equal(["groceries", "call mum"], Notes(fresh));
        Assert.Equal(OpenOutcome.RestoredFromBackup, fresh.Outcome);
        Assert.Equal(Path.Combine(Keep, "gili-2026-10-31.db"), fresh.RestoredFrom);
        Assert.Null(fresh.CorruptCopy);
    }

    [Fact]
    public void A_new_install_without_keep_copies_starts_empty()
    {
        using var fresh = Database.Open(NewData, Keep);
        Assert.Empty(Notes(fresh));
        Assert.Equal(OpenOutcome.Normal, fresh.Outcome);
        Assert.Null(fresh.RestoredFrom);
    }

    [Fact]
    public void An_existing_database_is_never_replaced_by_a_keep_copy()
    {
        using (var other = Database.Open(OldData, Keep))
        {
            AddNote(other, "from the other copy");
            Backups.Refresh(other, Keep, Today);
        }
        using (var own = Database.Open(NewData, Keep)) AddNote(own, "mine");

        using var reopened = Database.Open(NewData, Keep);
        Assert.Contains("mine", Notes(reopened));
        Assert.Equal(OpenOutcome.Normal, reopened.Outcome);
    }

    [Fact]
    public void A_corrupt_database_without_local_backups_comes_back_from_a_keep_copy()
    {
        using (var db = Database.Open(OldData, Keep))
        {
            AddNote(db, "keep me");
            Backups.Refresh(db, Keep, Today);
        }
        SqliteConnection.ClearAllPools();
        File.WriteAllText(Path.Combine(OldData, Database.FileName), "not a database");

        using var restored = Database.Open(OldData, Keep);
        Assert.Equal(["keep me"], Notes(restored));
        Assert.Equal(OpenOutcome.RestoredFromBackup, restored.Outcome);
        Assert.NotNull(restored.CorruptCopy);
    }

    [Fact]
    public void Refresh_overwrites_todays_copy()
    {
        using var db = Database.Open(OldData);
        AddNote(db, "first");
        Backups.Refresh(db, Keep, Today);
        AddNote(db, "second");
        Backups.Refresh(db, Keep, Today);

        Assert.Equal([Path.Combine(Keep, "gili-2026-10-31.db")], Backups.List(Keep));
        using var copy = Database.Open(NewData, Keep);
        Assert.Equal(["first", "second"], Notes(copy));
    }

    [Fact]
    public void Refresh_keeps_seven_days()
    {
        using var db = Database.Open(OldData);
        for (var day = 0; day < 10; day++) Backups.Refresh(db, Keep, Today.AddDays(day));
        Assert.Equal(Backups.Keep, Backups.List(Keep).Count);
    }

    [Fact]
    public void An_older_schema_is_copied_before_it_is_migrated()
    {
        using (var db = Database.Open(OldData))
        {
            AddNote(db, "written by 0.1.0");
            using var downgrade = db.Connection.CreateCommand();
            downgrade.CommandText = "ALTER TABLE todos DROP COLUMN due_time; ALTER TABLE shelf_items DROP COLUMN mode; " +
                "ALTER TABLE shelf_items DROP COLUMN folder; ALTER TABLE clipboard_history DROP COLUMN kind; ALTER TABLE clipboard_history DROP COLUMN name; " +
                "ALTER TABLE clipboard_history DROP COLUMN width; ALTER TABLE clipboard_history DROP COLUMN height; DROP TABLE project_tasks; DROP TABLE projects; UPDATE schema_version SET version = 1;";
            downgrade.ExecuteNonQuery();
        }
        SqliteConnection.ClearAllPools();

        using var migrated = Database.Open(OldData);
        var before = Path.Combine(OldData, Backups.FolderName, "gili-before-v5.db");
        Assert.Equal(before, migrated.MigrationCopy);
        Assert.True(File.Exists(before));
        using var copy = new SqliteConnection($"Data Source={before};Pooling=False");
        copy.Open();
        using var version = copy.CreateCommand();
        version.CommandText = "SELECT MAX(version) FROM schema_version";
        Assert.Equal(1L, version.ExecuteScalar());
    }

    [Fact]
    public void An_up_to_date_schema_makes_no_migration_copy()
    {
        using (Database.Open(OldData)) { }
        SqliteConnection.ClearAllPools();
        using var reopened = Database.Open(OldData);
        Assert.Null(reopened.MigrationCopy);
        Assert.False(Directory.Exists(Path.Combine(OldData, Backups.FolderName)));
    }

    [Fact]
    public void Settings_come_back_when_missing()
    {
        Directory.CreateDirectory(OldData);
        var store = new SettingsStore(OldData);
        store.Save(new Core.Settings.AppSettings { BarHeight = 40 });
        SettingsStore.CopyTo(store.FilePath, Keep);

        var fresh = new SettingsStore(NewData);
        Assert.True(fresh.RestoreFrom(Keep));
        Assert.Equal(40, fresh.Load().Settings.BarHeight);
    }

    [Fact]
    public void Every_save_refreshes_the_keep_copy()
    {
        var store = new SettingsStore(OldData, Keep);
        store.Save(new Core.Settings.AppSettings { BarHeight = 36 });
        store.Save(new Core.Settings.AppSettings { BarHeight = 38 });

        var fresh = new SettingsStore(NewData);
        Assert.True(fresh.RestoreFrom(Keep));
        Assert.Equal(38, fresh.Load().Settings.BarHeight);
    }

    [Fact]
    public void Existing_settings_are_not_replaced()
    {
        Directory.CreateDirectory(OldData);
        new SettingsStore(OldData).Save(new Core.Settings.AppSettings { BarHeight = 40 });
        SettingsStore.CopyTo(Path.Combine(OldData, "settings.json"), Keep);
        var own = new SettingsStore(NewData);
        own.Save(new Core.Settings.AppSettings { BarHeight = 30 });

        Assert.False(own.RestoreFrom(Keep));
        Assert.Equal(30, own.Load().Settings.BarHeight);
    }
}
