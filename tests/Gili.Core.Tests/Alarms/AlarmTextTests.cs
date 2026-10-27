using Gili.Core.Alarms;
using Gili.Core.Time;

namespace Gili.Core.Tests.Alarms;

public class AlarmTextTests
{
    private static readonly TimeZoneInfo Jakarta = TimeZoneInfo.CreateCustomTimeZone("plus7", TimeSpan.FromHours(7), "+07", "+07");
    private static Alarm Wake(Days repeat = Days.Weekdays, string label = "Wake up", string sound = "Radar") =>
        new(1, new TimeOnly(5, 30), repeat, label, sound, true, null);

    [Fact]
    public void Days_read_like_the_mockup()
    {
        Assert.Equal("Once", AlarmText.Days(Days.None));
        Assert.Equal("Every day", AlarmText.Days(Days.Every));
        Assert.Equal("Mon–Fri", AlarmText.Days(Days.Weekdays));
        Assert.Equal("Sat, Sun", AlarmText.Days(Days.Saturday | Days.Sunday));
        Assert.Equal("Mon, Wed, Fri", AlarmText.Days(Days.Monday | Days.Wednesday | Days.Friday));
    }

    [Fact]
    public void Row_detail_joins_days_and_sound() =>
        Assert.Equal("Mon–Fri · Radar", AlarmText.RowDetail(Wake(sound: "radar")));

    [Fact]
    public void Empty_label_reads_alarm() => Assert.Equal("Alarm", AlarmText.Label(Wake(label: " ")));

    [Fact]
    public void Next_heading_says_today_tomorrow_or_the_day()
    {
        var now = new DateTimeOffset(2026, 10, 27, 20, 0, 0, TimeSpan.FromHours(7)); // Tuesday evening
        Assert.Equal(("05:30", "Tomorrow · Wake up"),
            AlarmText.NextHeading(Wake() with { NextFireAt = new DateTimeOffset(2026, 10, 28, 5, 30, 0, TimeSpan.FromHours(7)) }, now, Jakarta, true));
        Assert.Equal(("21:00", "Today · Pack the bag"),
            AlarmText.NextHeading(new Alarm(2, new TimeOnly(21, 0), Days.None, "Pack the bag", "Soft", true, new DateTimeOffset(2026, 10, 27, 21, 0, 0, TimeSpan.FromHours(7))), now, Jakarta, true));
        Assert.Equal(("05:30", "Thu · Wake up"),
            AlarmText.NextHeading(Wake() with { NextFireAt = new DateTimeOffset(2026, 10, 29, 5, 30, 0, TimeSpan.FromHours(7)) }, now, Jakarta, true));
    }

    [Fact]
    public void No_alarm_on_reads_no_alarms_on() =>
        Assert.Equal(("--:--", "No alarms on"), AlarmText.NextHeading(null, DateTimeOffset.UtcNow, Jakarta, true));

    [Fact]
    public void Twelve_hour_times()
    {
        Assert.Equal("5:30 AM", AlarmText.Time(new TimeOnly(5, 30), use24Hour: false));
        Assert.Equal("2:30 PM", AlarmText.Time(new TimeOnly(14, 30), use24Hour: false));
        Assert.Equal("05:30", AlarmText.Time(new TimeOnly(5, 30), use24Hour: true));
    }

    [Fact]
    public void Card_titles_for_ringing_and_missed()
    {
        var alarm = new Alarm(1, new TimeOnly(14, 30), Days.Every, "Take medicine", "Radar", true, null);
        Assert.Equal("Alarm 14:30", AlarmText.CardTitle(alarm, missed: false, use24Hour: true));
        Assert.Equal("Missed alarm 14:30", AlarmText.CardTitle(alarm, missed: true, use24Hour: true));
    }

    [Theory]
    [InlineData("7:05", 7, 5)]
    [InlineData("0705", 7, 5)]
    [InlineData("07.05", 7, 5)]
    [InlineData(" 23:59 ", 23, 59)]
    public void Time_input_parses_loosely(string text, int hour, int minute) =>
        Assert.Equal(new TimeOnly(hour, minute), AlarmText.ParseTime(text));

    [Theory]
    [InlineData("25:00")]
    [InlineData("7:5")]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("12:60")]
    public void Time_input_rejects_nonsense(string text) => Assert.Null(AlarmText.ParseTime(text));
}
