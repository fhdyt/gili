using Gili.Core.Time;

namespace Gili.Core.Alarms;

/// <summary>When alarms ring (F-TOOL-05): the next time from their local time and days, snooze, and what comes due.</summary>
public static class AlarmSchedule
{
    /// <summary>An alarm that is on gets its next time; one that is off has none.</summary>
    public static Alarm Arm(Alarm alarm, DateTimeOffset now, TimeZoneInfo zone) =>
        alarm with { NextFireAt = alarm.Enabled ? AlarmRules.NextFire(alarm.Time, alarm.Repeat, now, zone) : null };

    /// <summary>
    /// Every alarm that is on and has reached its time, once each, oldest first; one asleep through its time (or through
    /// several of its days) comes once, marked missed (plan 2d decision 5).
    /// </summary>
    public static IReadOnlyList<(Alarm Alarm, bool Missed)> Due(IReadOnlyList<Alarm> alarms, DateTimeOffset now) =>
        [.. alarms.Where(a => a.Enabled && a.NextFireAt is { } at && at <= now)
            .OrderBy(a => a.NextFireAt).ThenBy(a => a.Id)
            .Select(a => (a, now - a.NextFireAt!.Value > Scheduler.MissedAfter))];

    /// <summary>After it rang: a one-off alarm turns off, a repeating one moves to its next day.</summary>
    public static Alarm AfterRing(Alarm alarm, DateTimeOffset now, TimeZoneInfo zone) =>
        alarm.Repeat == Days.None ? alarm with { Enabled = false, NextFireAt = null } : Arm(alarm, now, zone);

    public static Alarm Snooze(Alarm alarm, DateTimeOffset now) => alarm with { NextFireAt = AlarmRules.Snooze(now) };

    public static Alarm? Next(IReadOnlyList<Alarm> alarms) =>
        alarms.Where(a => a.Enabled && a.NextFireAt is not null).OrderBy(a => a.NextFireAt).ThenBy(a => a.Id).FirstOrDefault();
}
