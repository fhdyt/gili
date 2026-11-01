using Gili.Core.Claude;
using Gili.Data;
using Microsoft.Data.Sqlite;

namespace Gili.Core.Tests.Claude;

/// <summary>Revision 19: Claude's weekly limit reset, set by hand from Claude Desktop with its day, hour and minutes.</summary>
public sealed class ClaudeWeeklyTests : IDisposable
{
    private static readonly TimeSpan Jakarta = TimeSpan.FromHours(7);
    private DateTimeOffset _now = new(2026, 11, 1, 16, 30, 0, Jakarta);
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "gili-weekly-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private sealed class MemoryStore : IClaudeWeeklyStore
    {
        public ClaudeWeeklyReset? Row;

        public ClaudeWeeklyReset? Active() => Row is { Notified: false } ? Row : null;

        public ClaudeWeeklyReset Save(ClaudeWeeklyReset reset) => Row = reset.Id == 0 ? reset with { Id = 1 } : reset;
    }

    [Fact]
    public void The_days_to_pick_run_from_today_to_a_week_ahead() =>
        Assert.Equal(Enumerable.Range(0, 8).Select(d => new DateOnly(2026, 11, 1).AddDays(d)), ClaudeWeekly.Days(_now));

    [Theory]
    [InlineData(8, "04:00", 2026, 11, 8, 4, 0)]
    [InlineData(8, "4:00 AM", 2026, 11, 8, 4, 0)]
    [InlineData(1, "20:40", 2026, 11, 1, 20, 40)]
    [InlineData(1, "8:40 pm", 2026, 11, 1, 20, 40)]
    public void A_weekly_reset_typed_from_claude_desktop(int day, string time, int y, int mo, int d, int h, int mi) =>
        Assert.Equal(new DateTimeOffset(y, mo, d, h, mi, 0, Jakarta), ClaudeWeekly.ParseReset(new DateOnly(2026, 11, day), time, _now));

    [Theory]
    [InlineData(1, "16:00")]
    [InlineData(8, "17:00")]
    [InlineData(8, "4am")]
    [InlineData(8, "4")]
    [InlineData(8, "")]
    [InlineData(8, "24:00")]
    // Past, more than 7 days away, without its minutes, or not a time.
    public void A_weekly_reset_that_cant_be_is_refused(int day, string time) =>
        Assert.Null(ClaudeWeekly.ParseReset(new DateOnly(2026, 11, day), time, _now));

    [Fact]
    public void A_set_reset_rings_once_when_it_comes_and_is_then_unset()
    {
        var store = new MemoryStore();
        var weekly = new ClaudeWeekly(store, () => _now);
        weekly.Set(new DateTimeOffset(2026, 11, 8, 4, 0, 0, Jakarta));
        Assert.Equal(new DateTimeOffset(2026, 11, 8, 4, 0, 0, Jakarta), weekly.NextDeadline);
        Assert.Same(weekly.Current, store.Row);
        _now = new DateTimeOffset(2026, 11, 8, 3, 59, 59, Jakarta);
        Assert.False(weekly.Tick());
        _now = _now.AddSeconds(1);
        Assert.True(weekly.Tick());
        Assert.Null(weekly.Current);
        Assert.True(store.Row!.Notified);
        Assert.False(weekly.Tick());
    }

    [Fact]
    public void A_reset_that_came_while_gili_was_closed_ends_quietly()
    {
        var store = new MemoryStore();
        new ClaudeWeekly(store, () => _now).Set(new DateTimeOffset(2026, 11, 8, 4, 0, 0, Jakarta));
        _now = new DateTimeOffset(2026, 11, 8, 9, 0, 0, Jakarta);
        var again = new ClaudeWeekly(store, () => _now);
        again.Load();
        Assert.Null(again.Current);
        Assert.False(again.Tick());
    }

    [Fact]
    public void The_reset_survives_a_restart_and_a_new_one_replaces_it()
    {
        using (var db = Database.Open(_folder))
        {
            var weekly = new ClaudeWeekly(new ClaudeWeeklyStore(db), () => _now);
            weekly.Set(new DateTimeOffset(2026, 11, 8, 4, 0, 0, Jakarta));
            weekly.Set(new DateTimeOffset(2026, 11, 8, 5, 30, 0, Jakarta));
        }
        using (var db = Database.Open(_folder))
        {
            var weekly = new ClaudeWeekly(new ClaudeWeeklyStore(db), () => _now);
            weekly.Load();
            Assert.Equal(new DateTimeOffset(2026, 11, 8, 5, 30, 0, Jakarta), weekly.Current!.ResetsAt);
        }
    }

    [Fact]
    public void The_row_says_the_day_and_counts_days_then_hours()
    {
        var reset = new ClaudeWeeklyReset(1, new DateTimeOffset(2026, 11, 8, 4, 0, 0, Jakarta), false);
        var far = ClaudeWeeklyText.For(reset, _now, use24Hour: true);
        Assert.Equal(("Resets Sun 8 Nov, 04:00", "6d 11h", "Claude weekly limit · resets Sun 8 Nov, 04:00"), (far.Resets, far.Left, far.Tooltip));
        Assert.InRange(far.Elapsed, 0.07, 0.08);
        var near = ClaudeWeeklyText.For(reset, new DateTimeOffset(2026, 11, 7, 16, 39, 55, Jakarta), use24Hour: false);
        Assert.Equal(("Resets Sun 8 Nov, 4:00 AM", "11:20:05"), (near.Resets, near.Left));
    }

    [Fact]
    public void A_day_reads_as_the_stepper_shows_it() => Assert.Equal("Sun 8 Nov", ClaudeWeeklyText.Day(new DateOnly(2026, 11, 8)));
}
