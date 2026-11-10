using System.Globalization;

namespace Gili.Core.Claude;

/// <summary>Claude's weekly limit reset, as the claude_weekly table keeps it (PRD 10). The time is UTC.</summary>
public sealed record ClaudeWeeklyReset(long Id, DateTimeOffset ResetsAt, bool Notified);

/// <summary>Keeps the one weekly reset across restarts (PRD 9.2: stored as UTC).</summary>
public interface IClaudeWeeklyStore
{
    /// <summary>The newest reset, the last one set, or null when none ever was.</summary>
    ClaudeWeeklyReset? Active();

    /// <summary>Inserts a reset with Id 0, closing any other, else updates it; returns it with its id.</summary>
    ClaudeWeeklyReset Save(ClaudeWeeklyReset reset);
}

/// <summary>
/// Claude's weekly limit (PRD 6.3 "Batas pemakaian Claude", revisions 19 and 23): Gili can't read when it resets, so its day,
/// hour and minutes are set by hand from Claude Desktop, once. When that time comes, <see cref="Tick"/> says so once, for the
/// card, and the reset moves on a week, until it is edited.
/// </summary>
public sealed class ClaudeWeekly(IClaudeWeeklyStore? store, Func<DateTimeOffset> now)
{
    public static readonly TimeSpan Length = TimeSpan.FromDays(7);

    public ClaudeWeeklyReset? Current { get; private set; }

    public event Action? Changed;

    /// <summary>When the set reset comes.</summary>
    public DateTimeOffset? NextDeadline => Current?.ResetsAt;

    /// <summary>The form's stepper: the next or previous day, round the week.</summary>
    public static DayOfWeek StepDay(DayOfWeek day, int by) => (DayOfWeek)(((int)day + by + 7) % 7);

    /// <summary>
    /// The next time that day comes at the time typed with its hour and minutes ("04:00", "4:00 AM"): today while the time
    /// is still ahead, otherwise up to a week on; null when it is no time.
    /// </summary>
    public static DateTimeOffset? ParseReset(DayOfWeek day, string time, DateTimeOffset at)
    {
        if (ClaudeUsage.TimeOfDay(time) is not { } clock) return null;
        var date = DateOnly.FromDateTime(at.DateTime).AddDays(((int)day - (int)at.DayOfWeek + 7) % 7);
        var reset = new DateTimeOffset(date, clock, at.Offset);
        return reset > at ? reset : reset + Length;
    }

    /// <summary>Reads the reset kept from before; one that came while Gili was closed moves on quietly.</summary>
    public void Load()
    {
        Current = store?.Active();
        if (Current is { } reset && now() - reset.ResetsAt > ClaudeUsage.StaleAfter) MoveOn(reset);
        Changed?.Invoke();
    }

    /// <summary>The reset time shown in Claude Desktop, set by hand; it replaces the one set before.</summary>
    public void Set(DateTimeOffset reset)
    {
        Current = store?.Save(new ClaudeWeeklyReset(0, reset, false)) ?? new ClaudeWeeklyReset(0, reset, false);
        Changed?.Invoke();
    }

    /// <summary>True once, when the reset has just come: the card rings. One that came during sleep is quiet.</summary>
    public bool Tick()
    {
        if (Current is not { } reset || now() < reset.ResetsAt) return false;
        var fresh = now() - reset.ResetsAt <= ClaudeUsage.StaleAfter;
        MoveOn(reset);
        Changed?.Invoke();
        return fresh;
    }

    /// <summary>The demo's own reset, never stored (F-BAR-09).</summary>
    public void ShowDemo(ClaudeWeeklyReset? reset)
    {
        Current = reset;
        Changed?.Invoke();
    }

    /// <summary>The next reset still to come, whole weeks after this one, so the day and time set stay.</summary>
    private void MoveOn(ClaudeWeeklyReset reset)
    {
        var weeks = Math.Floor((now() - reset.ResetsAt) / Length) + 1;
        var next = reset with { ResetsAt = reset.ResetsAt + weeks * Length, Notified = false };
        Current = store?.Save(next) ?? next;
    }
}

/// <summary>What the Claude page's weekly row shows: Resets Sun 8 Nov, 04:00 and the time left, days then hours.</summary>
public sealed record ClaudeWeeklyView(string Resets, string Left, string Tooltip, double Elapsed);

public static class ClaudeWeeklyText
{
    public static ClaudeWeeklyView For(ClaudeWeeklyReset reset, DateTimeOffset now, bool use24Hour)
    {
        var left = reset.ResetsAt - now;
        if (left < TimeSpan.Zero) left = TimeSpan.Zero;
        var at = reset.ResetsAt.ToOffset(now.Offset);
        var when = Day(DateOnly.FromDateTime(at.DateTime)) + ", " + at.ToString(use24Hour ? "HH:mm" : "h:mm tt", CultureInfo.InvariantCulture);
        var text = left >= TimeSpan.FromDays(1) ? $"{(int)left.TotalDays}d {left.Hours}h" : ClaudeSlot.Running(left);
        return new("Resets " + when, text, "Claude weekly limit · resets " + when, Math.Clamp(1 - left / ClaudeWeekly.Length, 0, 1));
    }

    /// <summary>A day as the row shows it: Sun 8 Nov.</summary>
    public static string Day(DateOnly day) => day.ToString("ddd d MMM", CultureInfo.InvariantCulture);
}
