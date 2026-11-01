using Gili.Core.Claude;
using Gili.Data;
using Microsoft.Data.Sqlite;

namespace Gili.Core.Tests.Claude;

/// <summary>F-CLD-08: the Claude 5-hour window (PRD 6.3 "Batas pemakaian Claude", design 3j).</summary>
public sealed class ClaudeUsageTests : IDisposable
{
    private static readonly TimeSpan Jakarta = TimeSpan.FromHours(7);
    private DateTimeOffset _now = new(2026, 10, 31, 14, 23, 10, Jakarta);
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "gili-claude-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private sealed class MemoryStore : IClaudeWindowStore
    {
        public ClaudeUsageWindow? Row;

        public ClaudeUsageWindow? Active() => Row is { Notified: false } ? Row : null;

        public ClaudeUsageWindow Save(ClaudeUsageWindow window) => Row = window.Id == 0 ? window with { Id = 1 } : window;
    }

    [Fact]
    public void The_first_use_starts_a_window_on_its_ten_minutes()
    {
        // Claude's own resets fall on ten minutes (20:40, 20:10), not on the hour.
        var usage = new ClaudeUsage(null, () => _now);
        usage.Used();
        Assert.Equal(new DateTimeOffset(2026, 10, 31, 14, 20, 0, Jakarta), usage.Current!.StartedAt);
        Assert.Equal(new DateTimeOffset(2026, 10, 31, 19, 20, 0, Jakarta), usage.Current.ResetsAt);
        Assert.Equal(ClaudeWindowSource.Estimated, usage.Current.Source);
        _now = _now.AddHours(1);
        usage.Used();
        Assert.Equal(new DateTimeOffset(2026, 10, 31, 19, 20, 0, Jakarta), usage.Current.ResetsAt);
    }

    [Fact]
    public void The_window_ends_once_with_its_card_and_the_next_prompt_starts_another()
    {
        var usage = new ClaudeUsage(null, () => _now);
        usage.Used();
        _now = new DateTimeOffset(2026, 10, 31, 19, 19, 59, Jakarta);
        Assert.False(usage.Tick());
        _now = _now.AddSeconds(1);
        Assert.True(usage.Tick());
        Assert.Null(usage.Current);
        Assert.False(usage.Tick());
        _now = _now.AddMinutes(10);
        usage.Used();
        Assert.Equal(new DateTimeOffset(2026, 10, 31, 19, 30, 0, Jakarta), usage.Current!.StartedAt);
    }

    [Fact]
    public void A_window_that_ended_during_sleep_ends_quietly()
    {
        var usage = new ClaudeUsage(null, () => _now);
        usage.Used();
        _now = new DateTimeOffset(2026, 10, 31, 21, 0, 0, Jakarta);
        Assert.False(usage.Tick());
        Assert.Null(usage.Current);
    }

    [Fact]
    public void Use_after_the_window_ended_starts_another_before_its_tick()
    {
        // Issue 39: the laptop slept past 19:00, so the old window was still Current when the first prompt came.
        var store = new MemoryStore();
        var usage = new ClaudeUsage(store, () => _now);
        usage.Used();
        _now = new DateTimeOffset(2026, 11, 1, 7, 35, 52, Jakarta);
        usage.Used();
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 7, 30, 0, Jakarta), usage.Current!.StartedAt);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 12, 30, 0, Jakarta), usage.Current.ResetsAt);
        Assert.False(usage.Tick());
        Assert.Same(usage.Current, store.Row);
    }

    [Fact]
    public void A_limit_after_the_window_ended_starts_another_before_its_tick()
    {
        var usage = new ClaudeUsage(null, () => _now);
        usage.Used();
        _now = new DateTimeOffset(2026, 11, 1, 7, 35, 52, Jakarta);
        usage.LimitReached("You've hit your limit · resets 12pm");
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 7, 0, 0, Jakarta), usage.Current!.StartedAt);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 12, 0, 0, Jakarta), usage.Current.ResetsAt);
        Assert.True(usage.Current.LimitHit);
    }

    [Theory]
    [InlineData("UserPromptSubmit", true)]
    [InlineData("PreToolUse", true)]
    [InlineData("PostToolUse", true)]
    [InlineData("Stop", true)]
    [InlineData("SessionStart", false)]
    [InlineData("Notification", false)]
    [InlineData("SessionEnd", false)]
    public void Only_hooks_that_follow_a_request_to_claude_count_as_use(string hook, bool used) =>
        Assert.Equal(used, ClaudeUsage.IsUse(hook));

    [Fact]
    public void The_limit_marks_the_window_and_takes_claudes_reset_time()
    {
        var usage = new ClaudeUsage(null, () => _now);
        usage.Used();
        usage.LimitReached("5-hour limit reached ∙ resets 7:30pm");
        Assert.True(usage.Current!.LimitHit);
        Assert.Equal(new DateTimeOffset(2026, 10, 31, 19, 30, 0, Jakarta), usage.Current.ResetsAt);
        Assert.Equal(ClaudeWindowSource.Official, usage.Current.Source);
    }

    [Fact]
    public void A_limit_without_a_window_or_a_time_guesses_from_now()
    {
        var usage = new ClaudeUsage(null, () => _now);
        usage.LimitReached("API Error: Rate limit reached");
        Assert.Equal((14, 19, true), (usage.Current!.StartedAt.Hour, usage.Current.ResetsAt.Hour, usage.Current.LimitHit));
        Assert.Equal(ClaudeWindowSource.Estimated, usage.Current.Source);
    }

    [Theory]
    [InlineData("19:20", 19, 20)]
    [InlineData("7:20 PM", 19, 20)]
    [InlineData("7:20pm", 19, 20)]
    [InlineData(" 15:00 ", 15, 0)]
    public void A_reset_time_typed_from_claude_desktop(string text, int h, int mi) =>
        Assert.Equal(new DateTimeOffset(2026, 10, 31, h, mi, 0, Jakarta), ClaudeUsage.ParseReset(text, _now));

    [Theory]
    [InlineData("")]
    [InlineData("8")]
    [InlineData("7pm")]
    [InlineData("tonight")]
    [InlineData("14:00")]
    [InlineData("19:30")]
    [InlineData("25:00")]
    // Past, more than 5 hours away, without its minutes, or not a time: a window can't reset then.
    public void A_reset_time_that_cant_be_is_refused(string text) => Assert.Null(ClaudeUsage.ParseReset(text, _now));

    [Fact]
    public void A_reset_time_set_by_hand_starts_the_window_claude_runs()
    {
        var store = new MemoryStore();
        var usage = new ClaudeUsage(store, () => _now);
        usage.SetReset(new DateTimeOffset(2026, 10, 31, 15, 40, 0, Jakarta));
        Assert.Equal(new ClaudeUsageWindow(1, new(2026, 10, 31, 10, 40, 0, Jakarta), new(2026, 10, 31, 15, 40, 0, Jakarta),
            ClaudeWindowSource.Official, null, false), usage.Current);
        Assert.Same(usage.Current, store.Row);
        // Use within it changes nothing; the card rings at the time that was set.
        usage.Used();
        Assert.Equal(15, usage.Current!.ResetsAt.Hour);
        _now = new DateTimeOffset(2026, 10, 31, 15, 40, 0, Jakarta);
        Assert.True(usage.Tick());
    }

    [Fact]
    public void A_reset_time_set_by_hand_keeps_the_running_window_and_its_limit()
    {
        var usage = new ClaudeUsage(null, () => _now);
        usage.Used();
        usage.LimitReached("API Error: Rate limit reached");
        var id = usage.Current!.Id;
        usage.SetReset(new DateTimeOffset(2026, 10, 31, 18, 40, 0, Jakarta));
        Assert.Equal((id, true, ClaudeWindowSource.Official), (usage.Current!.Id, usage.Current.LimitHit, usage.Current.Source));
        Assert.Equal(new DateTimeOffset(2026, 10, 31, 13, 40, 0, Jakarta), usage.Current.StartedAt);
        Assert.Equal("Claude 5-hour window · resets at 18:40 · limit reached",
            ClaudeUsageText.For(usage.Current, _now, use24Hour: true).Tooltip);
    }

    [Theory]
    [InlineData("Claude AI usage limit reached|1793455200", 2026, 10, 31, 21, 0)]
    [InlineData("You've hit your limit · resets 7pm (Asia/Jakarta)", 2026, 10, 31, 19, 0)]
    [InlineData("Limit reached, resets at 19:45", 2026, 10, 31, 19, 45)]
    [InlineData("resets 9am", 2026, 11, 1, 9, 0)]
    [InlineData("resets 12am", 2026, 11, 1, 0, 0)]
    public void Reset_times_in_claudes_text(string text, int y, int mo, int d, int h, int mi) =>
        Assert.Equal(new DateTimeOffset(y, mo, d, h, mi, 0, Jakarta), ClaudeUsage.ResetIn(text, _now));

    [Theory]
    [InlineData(null)]
    [InlineData("API Error: Rate limit reached")]
    [InlineData("resets 7")]
    [InlineData("resets 13pm")]
    public void No_reset_time_when_the_text_has_none(string? text) => Assert.Null(ClaudeUsage.ResetIn(text, _now));

    [Fact]
    public void The_window_survives_a_restart()
    {
        var store = new MemoryStore();
        var usage = new ClaudeUsage(store, () => _now);
        usage.Used();
        _now = _now.AddHours(2);
        var again = new ClaudeUsage(store, () => _now);
        again.Load();
        Assert.Equal(19, again.Current!.ResetsAt.Hour);
    }

    [Fact]
    public void A_window_that_ended_while_gili_was_closed_ends_quietly_at_start()
    {
        var store = new MemoryStore();
        new ClaudeUsage(store, () => _now).Used();
        _now = _now.AddHours(6);
        var again = new ClaudeUsage(store, () => _now);
        again.Load();
        Assert.Null(again.Current);
        Assert.True(store.Row!.Notified);
    }

    [Fact]
    public void The_slot_counts_down_and_says_estimated()
    {
        var window = new ClaudeUsageWindow(1, new(2026, 10, 31, 14, 0, 0, Jakarta), new(2026, 10, 31, 19, 0, 0, Jakarta),
            ClaudeWindowSource.Estimated, null, false);
        var view = ClaudeUsageText.For(window, new DateTimeOffset(2026, 10, 31, 15, 47, 20, Jakarta), use24Hour: true);
        Assert.Equal(("Resets in", "3:12:40", "Claude 5-hour window · resets at 19:00 · Estimated", "Resets 19:00"),
            (view.Label, view.Left, view.Tooltip, view.Resets));
        Assert.InRange(view.Elapsed, 0.35, 0.36);
        var hit = ClaudeUsageText.For(window with { LimitHitAt = window.StartedAt, Source = ClaudeWindowSource.Official },
            new DateTimeOffset(2026, 10, 31, 17, 55, 48, Jakarta), use24Hour: false);
        Assert.Equal(("Limit reached", "1:04:12", "Claude 5-hour window · resets at 7:00 PM · limit reached", "Limit reached · Resets 7:00 PM"),
            (hit.Label, hit.Left, hit.Tooltip, hit.Resets));
    }

    [Fact]
    public void The_store_keeps_one_active_window_in_utc()
    {
        using (var db = Database.Open(_folder))
        {
            var store = new ClaudeWindowStore(db);
            var first = store.Save(new ClaudeUsageWindow(0, _now, _now.AddHours(5), ClaudeWindowSource.Estimated, null, false));
            store.Save(first with { LimitHitAt = _now.AddHours(1), Source = ClaudeWindowSource.Official });
            var second = store.Save(new ClaudeUsageWindow(0, _now.AddHours(5), _now.AddHours(10), ClaudeWindowSource.Estimated, null, false));
            Assert.NotEqual(first.Id, second.Id);
        }
        using (var db = Database.Open(_folder))
        {
            var active = new ClaudeWindowStore(db).Active()!;
            Assert.Equal(_now.AddHours(10), active.ResetsAt);
            Assert.Null(active.LimitHitAt);
            new ClaudeWindowStore(db).Save(active with { Notified = true });
            Assert.Null(new ClaudeWindowStore(db).Active());
        }
    }
}
