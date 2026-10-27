using Gili.Core.Alarms;
using Gili.Core.Time;

namespace Gili.Core.Tests.Alarms;

public class AlarmScheduleTests
{
    private static readonly TimeZoneInfo Jakarta = TimeZoneInfo.CreateCustomTimeZone("plus7", TimeSpan.FromHours(7), "+07", "+07");
    private static readonly DateTimeOffset Evening = new(2026, 10, 27, 20, 0, 0, TimeSpan.FromHours(7));

    private static Alarm At(int hour, int minute, Days repeat = Days.Every, bool enabled = true, long id = 1) =>
        new(id, new TimeOnly(hour, minute), repeat, "", "Radar", enabled, null);

    [Fact]
    public void Arming_an_enabled_alarm_sets_its_next_time() =>
        Assert.Equal(new DateTimeOffset(2026, 10, 28, 5, 30, 0, TimeSpan.FromHours(7)), AlarmSchedule.Arm(At(5, 30), Evening, Jakarta).NextFireAt);

    [Fact]
    public void Disabled_alarm_has_no_next_time() =>
        Assert.Null(AlarmSchedule.Arm(At(5, 30, enabled: false) with { NextFireAt = Evening }, Evening, Jakarta).NextFireAt);

    [Fact]
    public void Once_alarm_turns_off_after_ringing()
    {
        var rung = AlarmSchedule.AfterRing(At(20, 0, Days.None) with { NextFireAt = Evening }, Evening, Jakarta);
        Assert.False(rung.Enabled);
        Assert.Null(rung.NextFireAt);
    }

    [Fact]
    public void Repeating_alarm_moves_to_its_next_day() =>
        Assert.Equal(Evening.AddDays(1), AlarmSchedule.AfterRing(At(20, 0) with { NextFireAt = Evening }, Evening, Jakarta).NextFireAt);

    [Fact]
    public void Snooze_is_five_minutes_from_now() =>
        Assert.Equal(Evening.AddMinutes(5), AlarmSchedule.Snooze(At(20, 0) with { NextFireAt = Evening }, Evening).NextFireAt);

    [Fact]
    public void Same_minute_alarms_both_fire()
    {
        Alarm[] alarms = [At(20, 0, id: 1) with { NextFireAt = Evening }, At(20, 0, id: 2) with { NextFireAt = Evening }];
        Assert.Equal([1L, 2L], AlarmSchedule.Due(alarms, Evening.AddSeconds(1)).Select(d => d.Alarm.Id));
    }

    [Fact]
    public void Missed_while_asleep_rings_once_as_missed()
    {
        var due = AlarmSchedule.Due([At(5, 30) with { NextFireAt = Evening.AddDays(-3) }], Evening);
        Assert.Equal([(1L, true)], due.Select(d => (d.Alarm.Id, d.Missed)));
    }

    [Fact]
    public void Disabled_alarms_never_come_due() =>
        Assert.Empty(AlarmSchedule.Due([At(5, 30, enabled: false) with { NextFireAt = Evening.AddDays(-1) }], Evening));

    [Fact]
    public void Next_is_the_soonest_enabled()
    {
        Alarm[] alarms =
        [
            At(6, 0, id: 1) with { NextFireAt = Evening.AddHours(10) },
            At(5, 0, id: 2, enabled: false) with { NextFireAt = Evening.AddHours(1) },
            At(21, 0, id: 3) with { NextFireAt = Evening.AddHours(1) },
        ];
        Assert.Equal(3, AlarmSchedule.Next(alarms)!.Id);
    }
}
