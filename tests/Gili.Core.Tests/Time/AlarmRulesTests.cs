using Gili.Core.Time;

namespace Gili.Core.Tests.Time;

public class AlarmRulesTests
{
    private static readonly TimeZoneInfo Jakarta = TimeZoneInfo.CreateCustomTimeZone("plus7", TimeSpan.FromHours(7), "+07", "+07");
    private static readonly TimeZoneInfo Tokyo = TimeZoneInfo.CreateCustomTimeZone("plus9", TimeSpan.FromHours(9), "+09", "+09");
    private static readonly TimeZoneInfo NewYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    private static DateTimeOffset Local(int y, int mo, int d, int h, int mi, int offsetHours) => new(y, mo, d, h, mi, 0, TimeSpan.FromHours(offsetHours));

    [Fact]
    public void Weekday_alarm_skips_the_weekend()
    {
        var next = AlarmRules.NextFire(new TimeOnly(5, 30), Days.Weekdays, Local(2026, 10, 30, 6, 0, 7), Jakarta);
        Assert.Equal(Local(2026, 11, 2, 5, 30, 7), next);
    }

    [Fact]
    public void Once_alarm_fires_today_or_tomorrow()
    {
        Assert.Equal(Local(2026, 10, 27, 21, 0, 7), AlarmRules.NextFire(new TimeOnly(21, 0), Days.None, Local(2026, 10, 27, 20, 0, 7), Jakarta));
        Assert.Equal(Local(2026, 10, 28, 5, 30, 7), AlarmRules.NextFire(new TimeOnly(5, 30), Days.None, Local(2026, 10, 27, 20, 0, 7), Jakarta));
    }

    [Fact]
    public void An_alarm_at_this_very_minute_is_the_next_day()
    {
        var now = Local(2026, 10, 27, 5, 30, 7);
        Assert.Equal(Local(2026, 10, 28, 5, 30, 7), AlarmRules.NextFire(new TimeOnly(5, 30), Days.Every, now, Jakarta));
    }

    [Fact]
    public void Snooze_is_five_minutes()
    {
        var now = Local(2026, 10, 27, 5, 30, 7);
        Assert.Equal(now.AddMinutes(5), AlarmRules.Snooze(now));
    }

    [Fact]
    public void Restarted_timer_keeps_its_end()
    {
        var endsAt = Local(2026, 10, 27, 12, 25, 7);
        Assert.Equal(TimeSpan.FromMinutes(10), AlarmRules.Remaining(endsAt, endsAt.AddMinutes(-10)));
        Assert.Equal(TimeSpan.Zero, AlarmRules.Remaining(endsAt, endsAt.AddMinutes(3)));
    }

    [Fact]
    public void Time_zone_change_keeps_the_local_alarm_time()
    {
        var after = new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero);
        var inJakarta = AlarmRules.NextFire(new TimeOnly(5, 30), Days.Every, after, Jakarta);
        var inTokyo = AlarmRules.NextFire(new TimeOnly(5, 30), Days.Every, after, Tokyo);
        Assert.Equal(new TimeOnly(5, 30), TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(inJakarta, Jakarta).DateTime));
        Assert.Equal(new TimeOnly(5, 30), TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(inTokyo, Tokyo).DateTime));
        Assert.NotEqual(inJakarta, inTokyo);
    }

    [Fact]
    public void Spring_forward_gap_moves_to_the_first_valid_minute()
    {
        var next = AlarmRules.NextFire(new TimeOnly(2, 30), Days.None, new DateTimeOffset(2026, 3, 8, 5, 0, 0, TimeSpan.Zero), NewYork);
        Assert.Equal(new DateTimeOffset(2026, 3, 8, 7, 0, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void Fall_back_uses_the_first_occurrence()
    {
        var next = AlarmRules.NextFire(new TimeOnly(1, 30), Days.None, new DateTimeOffset(2026, 11, 1, 4, 0, 0, TimeSpan.Zero), NewYork);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 5, 30, 0, TimeSpan.Zero), next);
    }
}
