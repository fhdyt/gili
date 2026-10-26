using Gili.Core.Layout;

namespace Gili.Core.Tests.Layout;

public class BarMetricsTests
{
    [Theory]
    [InlineData(24, 12)]
    [InlineData(26, 13)]
    [InlineData(20, 11)]
    [InlineData(30, 15)]
    [InlineData(40, 15)]
    public void Island_font_is_half_the_island_height_within_11_to_15(int islandHeight, double expected) =>
        Assert.Equal(expected, BarMetrics.IslandFontSize(islandHeight));

    [Fact]
    public void Island_corner_radius_is_half_its_height() =>
        Assert.Equal(12, BarMetrics.IslandCornerRadius(24));

    [Theory]
    [InlineData(28, 24)]
    [InlineData(32, 26)]
    [InlineData(44, 38)]
    public void Module_hit_height_is_bar_minus_6_but_at_least_24(int barHeight, double expected) =>
        Assert.Equal(expected, BarMetrics.ModuleHitHeight(barHeight));
}
