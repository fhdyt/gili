using System.Globalization;

namespace Gili.Core.Claude;

/// <summary>Claude's weekly limit reset, as the claude_weekly table keeps it (PRD 10). The time is UTC.</summary>
public sealed record ClaudeWeeklyReset(long Id, DateTimeOffset ResetsAt, bool Notified);

/// <summary>Keeps the one weekly reset across restarts (PRD 9.2: stored as UTC).</summary>
public interface IClaudeWeeklyStore
{
    /// <summary>The newest reset not yet notified, or null.</summary>
    ClaudeWeeklyReset? Active();

    /// <summary>Inserts a reset with Id 0, closing any other, else updates it; returns it with its id.</summary>
    ClaudeWeeklyReset Save(ClaudeWeeklyReset reset);
}

/// <summary>
/// Claude's weekly limit (PRD 6.3 "Batas pemakaian Claude", revision 19): Gili can't read when it resets, so its day, hour
/// and minutes are set by hand from Claude Desktop. When that time comes, <see cref="Tick"/> says so once, for the card,
/// and the reset is unset: no next one is guessed.
/// </summary>
public sealed class ClaudeWeekly(IClaudeWeeklyStore? store, Func<DateTimeOffset> now)
{
    public static readonly TimeSpan Length = TimeSpan.FromDays(7);

    public ClaudeWeeklyReset? Current { get; private set; }

    public event Action? Changed;

    /// <summary>When the set reset comes.</summary>
    public DateTimeOffset? NextDeadline => Current?.ResetsAt;

    /// <summary>The days the form's stepper offers: today up to a week ahead.</summary>
    public static IReadOnlyList<DateOnly> Days(DateTimeOffset at)
    {
        var today = DateOnly.FromDateTime(at.DateTime);
        return [.. Enumerable.Range(0, 8).Select(today.AddDays)];
    }

    /// <summary>
    /// The day and the time typed with its hour and minutes ("04:00", "4:00 AM"); null when it is no time, already past,
    /// or more than a week away.
    /// </summary>
    public static DateTimeOffset? ParseReset(DateOnly day, string time, DateTimeOffset at)
    {
        if (ClaudeUsage.TimeOfDay(time) is not { } clock) return null;
        var reset = new DateTimeOffset(day, clock, at.Offset);
        return reset > at && reset - at <= Length ? reset : null;
    }

    /// <summary>Reads the reset kept from before; one that came while Gili was closed ends quietly.</summary>
    public void Load()
    {
        Current = store?.Active();
        if (Current is { } reset && now() - reset.ResetsAt > ClaudeUsage.StaleAfter) Finish();
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
        Finish();
        Changed?.Invoke();
        return fresh;
    }

    /// <summary>The demo's own reset, never stored (F-BAR-09).</summary>
    public void ShowDemo(ClaudeWeeklyReset? reset)
    {
        Current = reset;
        Changed?.Invoke();
    }

    private void Finish()
    {
        if (Current is { } reset) store?.Save(reset with { Notified = true });
        Current = null;
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

    /// <summary>A day as the stepper and the row show it: Sun 8 Nov.</summary>
    public static string Day(DateOnly day) => day.ToString("ddd d MMM", CultureInfo.InvariantCulture);
}
