using Gili.Core.Settings;
using Gili.Core.Timers;

namespace Gili.Core.Tests.Settings;

public class AppSettingsTests
{
    [Fact]
    public void Defaults_match_the_prd()
    {
        var s = new AppSettings();
        Assert.True(s.Use24HourClock);
        Assert.Equal(100, s.BarOpacity);
        Assert.Equal(GlassLevel.Strong, s.Glass);
        Assert.Equal(32, s.BarHeight);
        Assert.Equal(24, s.IslandHeight);
        Assert.True(s.Autostart);
    }

    [Fact]
    public void New_defaults_match_the_mockup()
    {
        var s = new AppSettings();
        Assert.Equal(100, s.BarOpacity);
        Assert.True(s.ShowGili && s.ShowSeconds && s.AmbientGlow && s.AmbientMotion);
        Assert.Equal(("Jakarta", -6.2088, 106.8456), (s.WeatherCity, s.WeatherLatitude, s.WeatherLongitude));
    }

    [Fact]
    public void Settings_keep_pomodoro_lengths_and_clamp_them()
    {
        var s = new AppSettings();
        Assert.Equal((25, 5, 15, 4), (s.PomodoroFocus, s.PomodoroBreak, s.PomodoroLongBreak, s.PomodoroRounds));
        Assert.Equal(PomodoroPlan.Default, s.Pomodoro);
        var clamped = (s with { PomodoroFocus = 500, PomodoroBreak = 0, PomodoroLongBreak = 99, PomodoroRounds = 1 }).Normalize();
        Assert.Equal((120, 1, 60, 2), (clamped.PomodoroFocus, clamped.PomodoroBreak, clamped.PomodoroLongBreak, clamped.PomodoroRounds));
        Assert.Equal(new PomodoroPlan(120, 1, 60, 2), clamped.Pomodoro);
    }

    [Fact]
    public void Sound_defaults_match_the_prd()
    {
        var s = new AppSettings();
        Assert.Equal(("Chime", 80, true), (s.TimerSound, s.AlertVolume, s.GraduallyIncrease));
    }

    [Fact]
    public void Alert_volume_is_clamped_to_0_100_and_the_sound_is_known()
    {
        Assert.Equal(100, new AppSettings { AlertVolume = 150 }.Normalize().AlertVolume);
        Assert.Equal(0, new AppSettings { AlertVolume = -5 }.Normalize().AlertVolume);
        Assert.Equal("Chime", new AppSettings { TimerSound = "Gong" }.Normalize().TimerSound);
    }

    [Theory]
    [InlineData(10, 28)]
    [InlineData(28, 28)]
    [InlineData(36, 36)]
    [InlineData(60, 44)]
    public void Bar_height_is_clamped_to_28_through_44(int input, int expected) =>
        Assert.Equal(expected, new AppSettings { BarHeight = input }.Normalize().BarHeight);

    [Fact]
    public void Island_height_never_exceeds_bar_height_minus_4() =>
        Assert.Equal(24, new AppSettings { BarHeight = 28, IslandHeight = 30 }.Normalize().IslandHeight);

    [Fact]
    public void Island_height_never_goes_below_20() =>
        Assert.Equal(20, new AppSettings { IslandHeight = 12 }.Normalize().IslandHeight);

    [Theory]
    [InlineData(10, 40)]
    [InlineData(78, 78)]
    [InlineData(150, 100)]
    public void Bar_opacity_is_clamped_to_40_through_100(int input, int expected) =>
        Assert.Equal(expected, new AppSettings { BarOpacity = input }.Normalize().BarOpacity);

    [Fact]
    public void Battery_settings_are_clamped()
    {
        Assert.Equal((20, 80), (new AppSettings().BatteryAlert, new AppSettings().UnplugReminder));
        Assert.Equal(5, new AppSettings { BatteryAlert = 2 }.Normalize().BatteryAlert);
        Assert.Equal(50, new AppSettings { BatteryAlert = 70 }.Normalize().BatteryAlert);
        Assert.Equal(50, new AppSettings { UnplugReminder = 30 }.Normalize().UnplugReminder);
        Assert.Equal(100, new AppSettings { UnplugReminder = 120 }.Normalize().UnplugReminder);
    }

    [Fact]
    public void Unknown_glass_value_falls_back_to_strong() =>
        Assert.Equal(GlassLevel.Strong, new AppSettings { Glass = (GlassLevel)9 }.Normalize().Glass);

    [Fact]
    public void Normalize_never_hides_every_tab()
    {
        var s = new AppSettings { HiddenLeft = LeftIcons.Timer, HiddenPages = PageSet.All }.Normalize();
        Assert.False(s.HiddenTabs.HasFlag(PageSet.Media));
        Assert.True(s.HiddenTabs.HasFlag(PageSet.Calendar));
    }

    [Fact]
    public void Hidden_tabs_join_pages_and_left_icons() =>
        Assert.Equal(PageSet.Notes | PageSet.Claude | PageSet.Calendar,
            new AppSettings { HiddenLeft = LeftIcons.Notes | LeftIcons.Claude | LeftIcons.Start, HiddenPages = PageSet.Calendar }.HiddenTabs);
}
