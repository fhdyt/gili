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
/// The Claude 5-hour window (PRD 6.3 "Batas pemakaian Claude", F-CLD-08, design 3j): it starts at the first prompt while
/// none runs, floored to the hour, and ends 5 hours later; the StopFailure hook with a rate_limit error marks the limit
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

    /// <summary>A prompt was sent in any session (UserPromptSubmit).</summary>
    public void Prompted()
    {
        if (Current is not null) return;
        var start = FloorToHour(now());
        Keep(new ClaudeUsageWindow(0, start, start + Length, ClaudeWindowSource.Estimated, null, false));
    }

    /// <summary>A turn ended on a rate limit (StopFailure, rate_limit); <paramref name="text"/> is Claude's error text.</summary>
    public void LimitReached(string? text)
    {
        var at = now();
        var reset = ResetIn(text, at);
        var window = Current;
        if (window is null)
        {
            var start = reset is { } r ? r - Length : FloorToHour(at);
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

    public static DateTimeOffset FloorToHour(DateTimeOffset time) => new(time.Year, time.Month, time.Day, time.Hour, 0, 0, time.Offset);

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
