using System.Globalization;
using System.Text.RegularExpressions;

namespace Gili.Core.Claude;

/// <summary>Where a window's reset time came from: Gili's guess from the first prompt, or Claude's own limit message.</summary>
public enum ClaudeWindowSource { Estimated, Official }

/// <summary>One Claude 5-hour usage window, as the claude_window table keeps it (PRD 10). Times are UTC.</summary>
public sealed record ClaudeUsageWindow(
    long Id,
    DateTimeOffset StartedAt,
    DateTimeOffset ResetsAt,
    ClaudeWindowSource Source,
    DateTimeOffset? LimitHitAt,
    bool Notified)
{
    public bool LimitHit => LimitHitAt is not null;
}

/// <summary>Keeps the one active window across restarts (PRD 9.2: stored as UTC).</summary>
public interface IClaudeWindowStore
{
    /// <summary>The newest window not yet notified, or null.</summary>
    ClaudeUsageWindow? Active();

    /// <summary>Inserts a window with Id 0, else updates it; returns it with its id.</summary>
    ClaudeUsageWindow Save(ClaudeUsageWindow window);
}

/// <summary>
/// The Claude 5-hour window (PRD 6.3 "Batas pemakaian Claude", F-CLD-08, design 3j): it starts at the first use while
/// none runs, floored to ten minutes, and ends 5 hours later, unless its reset time is set by hand from Claude Desktop; the StopFailure hook with a rate_limit error marks the limit
/// reached and may carry the real reset time. When the window ends, <see cref="Tick"/> says so once, for the card.
/// </summary>
public sealed class ClaudeUsage(IClaudeWindowStore? store, Func<DateTimeOffset> now)
{
    public static readonly TimeSpan Length = TimeSpan.FromHours(5);

    /// <summary>A window that ended longer ago than this, while Gili was closed or asleep, ends without its card.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(1);

    public ClaudeUsageWindow? Current { get; private set; }

    public event Action? Changed;

    /// <summary>When the running window ends.</summary>
    public DateTimeOffset? NextDeadline => Current?.ResetsAt;

    /// <summary>Reads the window kept from before; one that ended while Gili was closed ends quietly.</summary>
    public void Load()
    {
        Current = store?.Active();
        if (Current is { } window && now() - window.ResetsAt > StaleAfter) Finish();
        Changed?.Invoke();
    }

    /// <summary>
    /// The hooks that come right after a request to Claude, which is what starts a window: a prompt, a tool call, a turn's
    /// end. A turn that runs on past the reset starts the next window without a new prompt. A session starting or ending,
    /// or Claude waiting idle for input, sends nothing (issue 39).
    /// </summary>
    public static bool IsUse(string hook) => hook is "UserPromptSubmit" or "PreToolUse" or "PostToolUse" or "Stop";

    /// <summary>Claude was used in any session (<see cref="IsUse"/>).</summary>
    public void Used()
    {
        if (Running(now()) is not null) return;
        var start = WindowStart(now());
        Keep(new ClaudeUsageWindow(0, start, start + Length, ClaudeWindowSource.Estimated, null, false));
    }

    /// <summary>A turn ended on a rate limit (StopFailure, rate_limit); <paramref name="text"/> is Claude's error text.</summary>
    public void LimitReached(string? text)
    {
        var at = now();
        var reset = ResetIn(text, at);
        // A reset days away is the weekly limit's, never this window's (revision 19).
        if (reset - at > Length) return;
        var window = Running(at);
        if (window is null)
        {
            var start = reset is { } r ? r - Length : WindowStart(at);
            window = new ClaudeUsageWindow(0, start, reset ?? start + Length, reset is null ? ClaudeWindowSource.Estimated : ClaudeWindowSource.Official,
                null, false);
        }
        else if (reset is { } r)
        {
            window = window with { ResetsAt = r, Source = ClaudeWindowSource.Official };
        }
        Keep(window with { LimitHitAt = window.LimitHitAt ?? at });
    }

    /// <summary>True once, when the running window has just ended: the card rings. A stale end (after sleep) is quiet.</summary>
    public bool Tick()
    {
        if (Current is not { } window || now() < window.ResetsAt) return false;
        var fresh = now() - window.ResetsAt <= StaleAfter;
        Finish();
        Changed?.Invoke();
        return fresh;
    }

    /// <summary>
    /// A reset time typed from Claude Desktop with its hour and minutes ("20:40", "8:40 PM"), as the next such time after
    /// <paramref name="at"/>; null without minutes, or when not within the next 5 hours, so no running window could reset then.
    /// </summary>
    public static DateTimeOffset? ParseReset(string text, DateTimeOffset at)
    {
        if (TimeOfDay(text) is not { } time) return null;
        var reset = new DateTimeOffset(DateOnly.FromDateTime(at.DateTime), time, at.Offset);
        if (reset <= at) reset = reset.AddDays(1);
        return reset - at <= Length ? reset : null;
    }

    /// <summary>A time typed with its hour and minutes, "20:40" or "8:40 PM"; null for "8pm", "8" or anything else.</summary>
    public static TimeOnly? TimeOfDay(string text)
    {
        var clock = Regex.Match(text, @"^\s*(\d{1,2}):(\d{2})\s*(?:([ap])\.?\s*m\.?)?\s*$", RegexOptions.IgnoreCase);
        if (!clock.Success) return null;
        var hour = int.Parse(clock.Groups[1].Value, CultureInfo.InvariantCulture);
        var minute = int.Parse(clock.Groups[2].Value, CultureInfo.InvariantCulture);
        if (clock.Groups[3].Success)
        {
            if (hour is < 1 or > 12) return null;
            hour = hour % 12 + (char.ToLowerInvariant(clock.Groups[3].Value[0]) == 'p' ? 12 : 0);
        }
        return hour > 23 || minute > 59 ? null : new TimeOnly(hour, minute);
    }

    /// <summary>
    /// The reset time shown in Claude Desktop, set by hand (revision 18): Gili can't read it, so this window is Claude's own,
    /// 5 hours up to it, no longer Estimated. A running window keeps its id and its Limit reached.
    /// </summary>
    public void SetReset(DateTimeOffset reset)
    {
        var start = reset - Length;
        Keep(Running(now()) is { } window
            ? window with { StartedAt = start, ResetsAt = reset, Source = ClaudeWindowSource.Official }
            : new ClaudeUsageWindow(0, start, reset, ClaudeWindowSource.Official, null, false));
    }

    /// <summary>The demo's own window, never stored (F-BAR-09).</summary>
    public void ShowDemo(ClaudeUsageWindow? window)
    {
        Current = window;
        Changed?.Invoke();
    }

    /// <summary>
    /// The reset time in Claude's limit text: the old "…limit reached|1730000000" form, or "resets 7pm", "resets 7:30pm",
    /// "resets at 19:00", read as the next such time from <paramref name="at"/> in its offset.
    /// </summary>
    public static DateTimeOffset? ResetIn(string? text, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var epoch = Regex.Match(text, @"\|(\d{9,11})\b");
        if (epoch.Success && long.TryParse(epoch.Groups[1].Value, CultureInfo.InvariantCulture, out var seconds))
            return DateTimeOffset.FromUnixTimeSeconds(seconds).ToOffset(at.Offset);
        var clock = Regex.Match(text, @"\bresets?\s+(?:at\s+)?(\d{1,2})(?::(\d{2}))?\s*([ap]\.?m\.?)?", RegexOptions.IgnoreCase);
        if (!clock.Success) return null;
        var hour = int.Parse(clock.Groups[1].Value, CultureInfo.InvariantCulture);
        var minute = clock.Groups[2].Success ? int.Parse(clock.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
        if (clock.Groups[3].Success)
        {
            if (hour is < 1 or > 12) return null;
            var pm = char.ToLowerInvariant(clock.Groups[3].Value[0]) == 'p';
            hour = hour % 12 + (pm ? 12 : 0);
        }
        else if (!clock.Groups[2].Success)
        {
            // "resets 7" without am/pm or minutes is too vague to trust.
            return null;
        }
        if (hour > 23 || minute > 59) return null;
        var reset = new DateTimeOffset(at.Year, at.Month, at.Day, hour, minute, 0, at.Offset);
        return reset <= at ? reset.AddDays(1) : reset;
    }

    /// <summary>A window's start from its first use: down to its ten minutes, where Claude's own resets fall (20:40, 20:10).</summary>
    public static DateTimeOffset WindowStart(DateTimeOffset time) =>
        new(time.Year, time.Month, time.Day, time.Hour, time.Minute / 10 * 10, 0, time.Offset);

    /// <summary>
    /// The window still running at <paramref name="at"/>. One past its reset is over even before <see cref="Tick"/> sees it:
    /// the reset timer runs late after sleep, and use in that gap starts the next window (issue 39).
    /// </summary>
    private ClaudeUsageWindow? Running(DateTimeOffset at) => Current is { } window && at < window.ResetsAt ? window : null;

    private void Finish()
    {
        if (Current is { } window) store?.Save(window with { Notified = true });
        Current = null;
    }

    private void Keep(ClaudeUsageWindow window)
    {
        Current = store?.Save(window) ?? window;
        Changed?.Invoke();
    }
}

/// <summary>What the slot, the tooltip and the Claude page's summary row show for the window (mockup limTip, limRow).</summary>
public sealed record ClaudeUsageView(bool LimitHit, string Label, string Left, string Tooltip, string Resets, double Elapsed);

public static class ClaudeUsageText
{
    public static ClaudeUsageView For(ClaudeUsageWindow window, DateTimeOffset now, bool use24Hour)
    {
        var left = window.ResetsAt - now;
        if (left < TimeSpan.Zero) left = TimeSpan.Zero;
        var at = Clock(window.ResetsAt.ToOffset(now.Offset), use24Hour);
        var tooltip = $"Claude 5-hour window · resets at {at}" + (window.LimitHit ? " · limit reached" : "")
            + (window.Source == ClaudeWindowSource.Estimated ? " · Estimated" : "");
        var length = window.ResetsAt - window.StartedAt;
        var elapsed = length <= TimeSpan.Zero ? 1 : Math.Clamp(1 - left / length, 0, 1);
        return new(window.LimitHit, window.LimitHit ? "Limit reached" : "Resets in", ClaudeSlot.Running(left), tooltip,
            (window.LimitHit ? "Limit reached · " : "") + "Resets " + at, elapsed);
    }

    private static string Clock(DateTimeOffset time, bool use24Hour) =>
        time.ToString(use24Hour ? "HH:mm" : "h:mm tt", CultureInfo.InvariantCulture);
}
