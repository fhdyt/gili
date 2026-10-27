using Gili.Core.Alarms;
using Gili.Core.Time;
using Gili.Data;
using Microsoft.Data.Sqlite;

namespace Gili.Core.Tests.Data;

public sealed class AlarmStoreTests : IDisposable
{
    private static readonly DateTimeOffset Next = new(2026, 10, 27, 22, 30, 0, TimeSpan.Zero);
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "gili-alarms-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private static Alarm Wake => new(0, new TimeOnly(5, 30), Days.Weekdays, "Wake up", "Radar", true, Next);

    [Fact]
    public void Added_alarm_survives_a_restart()
    {
        Alarm added;
        using (var db = Database.Open(_folder)) added = new AlarmStore(db).Add(Wake);
        SqliteConnection.ClearAllPools();
        using var reopened = Database.Open(_folder);
        Assert.True(added.Id > 0);
        Assert.Equal([added], new AlarmStore(reopened).All());
    }

    [Fact]
    public void Update_changes_every_field()
    {
        using var db = Database.Open(_folder);
        var store = new AlarmStore(db);
        var added = store.Add(Wake);
        var changed = added with { Time = new TimeOnly(6, 15), Repeat = Days.Saturday, Label = "Run", Sound = "Soft", Enabled = false, NextFireAt = null };
        store.Update(changed);
        Assert.Equal([changed], store.All());
    }

    [Fact]
    public void Delete_removes_it()
    {
        using var db = Database.Open(_folder);
        var store = new AlarmStore(db);
        var added = store.Add(Wake);
        store.Delete(added.Id);
        Assert.Empty(store.All());
    }

    [Fact]
    public void All_is_sorted_by_time()
    {
        using var db = Database.Open(_folder);
        var store = new AlarmStore(db);
        store.Add(Wake with { Time = new TimeOnly(21, 0) });
        store.Add(Wake with { Time = new TimeOnly(5, 30) });
        store.Add(Wake with { Time = new TimeOnly(14, 30) });
        Assert.Equal([new TimeOnly(5, 30), new TimeOnly(14, 30), new TimeOnly(21, 0)], store.All().Select(a => a.Time));
    }

    [Fact]
    public void Sound_is_stored_lowercase_and_read_by_name()
    {
        using var db = Database.Open(_folder);
        var store = new AlarmStore(db);
        store.Add(Wake with { Sound = "PING" });
        using var raw = db.Connection.CreateCommand();
        raw.CommandText = "SELECT sound FROM alarms";
        Assert.Equal("ping", raw.ExecuteScalar());
        Assert.Equal("Ping", store.All()[0].Sound);
    }
}
