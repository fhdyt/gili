namespace Gili.Core.Time;

/// <summary>An alarm's repeat days, stored as a bitmask: Monday is bit 0, Sunday bit 6; None rings once.</summary>
[Flags]
public enum Days
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,
    Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday,
    Every = Weekdays | Saturday | Sunday,
}

/// <summary>When alarms and timers fire (F-TOOL-01, F-TOOL-05). Alarm times are local; the result is UTC.</summary>
public static class AlarmRules
{
    public static readonly TimeSpan SnoozeLength = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The first local <paramref name="time"/> on an allowed day strictly after <paramref name="afterUtc"/>. A time the
    /// clock skips (spring forward) moves to the first minute that exists; a time it repeats uses the first one
    /// (plan 2a decision 2).
    /// </summary>
    public static DateTimeOffset NextFire(TimeOnly time, Days repeat, DateTimeOffset afterUtc, TimeZoneInfo zone)
    {
        var start = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(afterUtc, zone).DateTime);
        for (var day = 0; day <= 8; day++)
        {
            var date = start.AddDays(day);
            if (repeat != Days.None && !repeat.HasFlag(DayOf(date.DayOfWeek))) continue;
            var candidate = At(date, time, zone);
            if (candidate > afterUtc) return candidate;
        }
        throw new ArgumentException("No day is allowed", nameof(repeat));
    }

    /// <summary>A local date and time in UTC, with the same rules for skipped and repeated clock times.</summary>
    public static DateTimeOffset At(DateOnly date, TimeOnly time, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(local)) local = local.AddMinutes(1);
        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }

    public static DateTimeOffset Snooze(DateTimeOffset now) => now + SnoozeLength;

    /// <summary>A running timer's time left from its stored end, so it stays right across restarts.</summary>
    public static TimeSpan Remaining(DateTimeOffset endsAtUtc, DateTimeOffset now) =>
        endsAtUtc > now ? endsAtUtc - now : TimeSpan.Zero;

    public static Days DayOf(DayOfWeek day) => day == DayOfWeek.Sunday ? Days.Sunday : (Days)(1 << ((int)day - 1));
}
