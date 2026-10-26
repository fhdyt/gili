using Gili.Core.Modules;

namespace Gili.Core.Tests.Modules;

public class RightRulesTests
{
    [Fact]
    public void Partly_cloudy_by_day_is_cloud_sun() =>
        Assert.Equal(("Cloudy", "Partly cloudy", WeatherIcon.CloudSun), WeatherText.For(2, day: true));

    [Fact]
    public void Clear_at_night_is_a_moon() => Assert.Equal(WeatherIcon.Moon, WeatherText.For(0, day: false).Icon);

    [Fact]
    public void Thunderstorm_codes_are_storm() => Assert.Equal(("Storm", "Thunderstorm", WeatherIcon.Storm), WeatherText.For(95, day: true));

    [Fact]
    public void Rain_and_snow_codes()
    {
        Assert.Equal(WeatherIcon.Rain, WeatherText.For(63, true).Icon);
        Assert.Equal(WeatherIcon.Rain, WeatherText.For(81, true).Icon);
        Assert.Equal("Showers", WeatherText.For(81, true).Label);
        Assert.Equal(WeatherIcon.Snow, WeatherText.For(85, true).Icon);
        Assert.Equal(WeatherIcon.Drizzle, WeatherText.For(53, true).Icon);
        Assert.Equal(WeatherIcon.Fog, WeatherText.For(48, true).Icon);
    }

    [Fact]
    public void Tooltip_reads_like_the_mockup() =>
        Assert.Equal("Partly cloudy · 31° / 24° · Jakarta", WeatherText.Tooltip("Partly cloudy", 31.4, 23.6, "Jakarta"));

    [Fact]
    public void Temperature_is_rounded_with_a_degree_sign() => Assert.Equal("29°", WeatherText.Temperature(28.6));

    [Theory]
    [InlineData(100, "Strong")]
    [InlineData(75, "Strong")]
    [InlineData(74, "Good")]
    [InlineData(50, "Good")]
    [InlineData(49, "Fair")]
    [InlineData(25, "Fair")]
    [InlineData(24, "Weak")]
    public void Signal_strength_labels(int quality, string label) => Assert.Equal(label, WifiText.Strength(quality));

    [Fact]
    public void Long_network_names_are_cut_at_16()
    {
        Assert.Equal("Home_5G", WifiText.Name("Home_5G"));
        Assert.Equal("Kopi Kenangan C…", WifiText.Name("Kopi Kenangan Cikini"));
    }

    [Fact]
    public void Zero_volume_shows_mute() => Assert.Equal(VolumeLevel.Mute, VolumeText.Level(0, muted: false));

    [Theory]
    [InlineData(33, VolumeLevel.Low)]
    [InlineData(34, VolumeLevel.Mid)]
    [InlineData(66, VolumeLevel.Mid)]
    [InlineData(67, VolumeLevel.High)]
    public void Volume_levels_split_at_34_and_67(int percent, VolumeLevel level) => Assert.Equal(level, VolumeText.Level(percent, false));

    [Fact]
    public void Volume_tooltip()
    {
        Assert.Equal("Volume 46%", VolumeText.Tooltip(46, false));
        Assert.Equal("Muted", VolumeText.Tooltip(46, true));
    }

    [Fact]
    public void Battery_line_variants()
    {
        Assert.Equal("64% · 2 h 10 min", BatteryText.Line(64, false, TimeSpan.FromMinutes(130)));
        Assert.Equal("64% · 45 min", BatteryText.Line(64, false, TimeSpan.FromMinutes(45)));
        Assert.Equal("Charging · 64%", BatteryText.Line(64, true, null));
        Assert.Equal("64%", BatteryText.Line(64, false, null));
        Assert.Equal("", BatteryText.Line(null, false, null));
    }

    [Fact]
    public void Overflow_hides_weather_first_then_network()
    {
        var modules = new[] { ("weather", 70.0), ("net", 66.0), ("cpu", 36.0), ("ram", 36.0), ("wifi", 90.0), ("clock", 60.0) };
        Assert.Empty(RightOverflow.Hide(modules, 400));
        Assert.Equal(["weather"], RightOverflow.Hide(modules, 340));
        Assert.Equal(["weather", "net"], RightOverflow.Hide(modules, 300));
    }

    [Fact]
    public void Controls_never_overflow()
    {
        var modules = new[] { ("weather", 70.0), ("net", 66.0), ("cpu", 36.0), ("ram", 36.0), ("wifi", 90.0), ("clock", 60.0) };
        Assert.Equal(["weather", "net", "ram", "cpu"], RightOverflow.Hide(modules, 10));
    }

    [Fact]
    public void Retry_backs_off_5s_30s_then_2min()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), ProviderRetry.Delay(1));
        Assert.Equal(TimeSpan.FromSeconds(30), ProviderRetry.Delay(2));
        Assert.Equal(TimeSpan.FromMinutes(2), ProviderRetry.Delay(3));
        Assert.Equal(TimeSpan.FromMinutes(2), ProviderRetry.Delay(9));
    }
}
