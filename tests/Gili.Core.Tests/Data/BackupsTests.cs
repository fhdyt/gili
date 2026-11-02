using Gili.Data;
using Microsoft.Data.Sqlite;

namespace Gili.Core.Tests.Data;

public sealed class BackupsTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 27);
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "gili-backup-" + Guid.NewGuid().ToString("N"));

    private string BackupFolder => Path.Combine(_folder, "backups");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
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
        select.CommandText = "SELECT body FROM notes";
        using var reader = select.ExecuteReader();
        var bodies = new List<string>();
        while (reader.Read()) bodies.Add(reader.GetString(0));
        return bodies;
    }

    [Fact]
    public void One_backup_per_day()
    {
        using var db = Database.Open(_folder);
        Assert.True(Backups.BackUpIfDue(db, BackupFolder, Today));
        Assert.False(Backups.BackUpIfDue(db, BackupFolder, Today));
        Assert.Equal([Path.Combine(BackupFolder, "gili-2026-10-27.db")], Backups.List(BackupFolder));
    }

    [Fact]
    public void Backups_leave_the_clipboard_history_out()
    {
        using var db = Database.Open(_folder);
        AddNote(db, "kept");
        new ClipboardHistoryStore(db).Save(new Gili.Core.Clipboard.ClipboardEntry(0, "secret", null, DateTimeOffset.UtcNow));
        Backups.BackUpIfDue(db, BackupFolder, Today);
        using var copy = new SqliteConnection($"Data Source={Backups.List(BackupFolder)[0]};Pooling=False");
        copy.Open();
        using var count = copy.CreateCommand();
        count.CommandText = "SELECT (SELECT count(*) FROM clipboard_history), (SELECT count(*) FROM notes)";
        using var reader = count.ExecuteReader();
        reader.Read();
        Assert.Equal((0L, 1L), (reader.GetInt64(0), reader.GetInt64(1)));
        Assert.Single(new ClipboardHistoryStore(db).All());
    }

    [Fact]
    public void Only_seven_backups_are_kept()
    {
        using var db = Database.Open(_folder);
        for (var day = 0; day < 10; day++) Backups.BackUpIfDue(db, BackupFolder, Today.AddDays(day));
        var kept = Backups.List(BackupFolder);
        Assert.Equal(Backups.Keep, kept.Count);
        Assert.Equal("gili-2026-11-05.db", Path.GetFileName(kept[0]));
        Assert.Equal("gili-2026-10-30.db", Path.GetFileName(kept[^1]));
    }

    [Fact]
    public void Other_files_in_backups_are_left_alone()
    {
        using var db = Database.Open(_folder);
        Directory.CreateDirectory(BackupFolder);
        var mine = Path.Combine(BackupFolder, "my-copy.db");
        File.WriteAllText(mine, "keep me");
        for (var day = 0; day < 10; day++) Backups.BackUpIfDue(db, BackupFolder, Today.AddDays(day));
        Assert.True(File.Exists(mine));
    }

    [Fact]
    public void A_backup_dated_in_the_future_is_not_overwritten()
    {
        using var db = Database.Open(_folder);
        Backups.BackUpIfDue(db, BackupFolder, Today.AddDays(3));
        var future = Path.Combine(BackupFolder, "gili-2026-10-30.db");
        var written = File.GetLastWriteTimeUtc(future);
        Assert.True(Backups.BackUpIfDue(db, BackupFolder, Today));
        Assert.Equal(written, File.GetLastWriteTimeUtc(future));
    }

    [Fact]
    public void Corrupt_database_is_restored_from_the_latest_good_backup()
    {
        using (var db = Database.Open(_folder))
        {
            AddNote(db, "saved yesterday");
            Backups.BackUpIfDue(db, BackupFolder, Today);
        }
        SqliteConnection.ClearAllPools();
        File.WriteAllText(Path.Combine(_folder, Database.FileName), "this is not a database");
        foreach (var side in Directory.GetFiles(_folder, "gili.db-*")) File.Delete(side);

        using var restored = Database.Open(_folder);
        Assert.Equal(OpenOutcome.RestoredFromBackup, restored.Outcome);
        Assert.Equal(["saved yesterday"], Notes(restored));
        Assert.True(File.Exists(restored.CorruptCopy));
    }

    [Fact]
    public void Corrupt_database_without_backups_starts_new()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, Database.FileName), "this is not a database");
        using var db = Database.Open(_folder);
        Assert.Equal(OpenOutcome.CreatedNew, db.Outcome);
        Assert.Empty(Notes(db));
        Assert.True(File.Exists(db.CorruptCopy));
    }
}
