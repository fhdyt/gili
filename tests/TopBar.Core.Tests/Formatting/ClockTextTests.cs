using TopBar.Core.Formatting;

namespace TopBar.Core.Tests.Formatting;

public class ClockTextTests
{
    private static readonly DateTime Evening = new(2026, 10, 26, 19, 38, 0);

    [Fact]
    public void Time_uses_24_hour_digits_without_seconds() =>
        Assert.Equal("19:38", ClockText.Time(Evening, use24Hour: true));

    [Fact]
    public void Morning_in_24_hour_mode_keeps_the_leading_zero() =>
        Assert.Equal("05:30", ClockText.Time(new DateTime(2026, 10, 27, 5, 30, 0), use24Hour: true));

    [Fact]
    public void Time_in_12_hour_mode_has_am_pm() =>
        Assert.Equal("7:38 PM", ClockText.Time(Evening, use24Hour: false));

    [Fact]
    public void Midnight_in_12_hour_mode_is_12_am() =>
        Assert.Equal("12:05 AM", ClockText.Time(new DateTime(2026, 10, 27, 0, 5, 0), use24Hour: false));

    [Fact]
    public void Date_is_short_english_day_day_number_and_month() =>
        Assert.Equal("Mon 26 Oct", ClockText.Date(Evening));

    [Theory]
    [InlineData(20, 250, 39_750)]
    [InlineData(0, 0, 60_000)]
    [InlineData(59, 999, 1)]
    public void UntilNextMinute_lands_on_the_next_minute_boundary(int second, int millisecond, int expectedMs)
    {
        var now = new DateTime(2026, 10, 26, 19, 38, second, millisecond);
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), ClockText.UntilNextMinute(now));
    }
}
