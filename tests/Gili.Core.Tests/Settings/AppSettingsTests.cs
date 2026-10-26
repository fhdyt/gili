using Gili.Core.Settings;

namespace Gili.Core.Tests.Settings;

public class AppSettingsTests
{
    [Fact]
    public void Defaults_match_the_prd()
    {
        var s = new AppSettings();
        Assert.True(s.Use24HourClock);
        Assert.Equal(78, s.BarOpacity);
        Assert.Equal(GlassLevel.Strong, s.Glass);
        Assert.Equal(32, s.BarHeight);
        Assert.Equal(24, s.IslandHeight);
        Assert.True(s.Autostart);
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
    public void Unknown_glass_value_falls_back_to_strong() =>
        Assert.Equal(GlassLevel.Strong, new AppSettings { Glass = (GlassLevel)9 }.Normalize().Glass);
}
