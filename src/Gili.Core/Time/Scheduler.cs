namespace Gili.Core.Time;

/// <summary>Something that fires at an absolute moment, stored in UTC (PRD 9.3).</summary>
public sealed record ScheduledItem(string Id, DateTimeOffset FireAtUtc);

/// <summary>A due item; <paramref name="Missed"/> when it came due over a minute ago (asleep, Gili closed).</summary>
public sealed record DueItem(ScheduledItem Item, bool Missed);

/// <summary>
/// What is due and when the next thing is (PRD 9.3). It keeps no timer of its own: the app sets one timer to
/// <see cref="Next"/> and asks again on start, on waking and when the clock or time zone changes (plan 2a decision 3).
/// </summary>
public static class Scheduler
{
    /// <summary>Later than this past its time, an item counts as missed and says so (F-TOOL-05 "Missed").</summary>
    public static readonly TimeSpan MissedAfter = TimeSpan.FromSeconds(60);

    public static IReadOnlyList<DueItem> Due(IEnumerable<ScheduledItem> items, DateTimeOffset now) =>
        [.. items.Where(i => i.FireAtUtc <= now)
            .OrderBy(i => i.FireAtUtc)
            .Select(i => new DueItem(i, now - i.FireAtUtc > MissedAfter))];

    public static DateTimeOffset? Next(IEnumerable<ScheduledItem> items, DateTimeOffset now) =>
        items.Where(i => i.FireAtUtc > now).Select(i => (DateTimeOffset?)i.FireAtUtc).Min();
}
