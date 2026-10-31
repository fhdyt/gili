using Gili.Core.Island;
using Gili.Core.Settings;

namespace Gili.Core.Tests.Settings;

/// <summary>F-BAR-10: the bar goes to the bottom only once the Windows taskbar is hidden (design 3g).</summary>
public sealed class BarPositionTests
{
    private static readonly AppSettings Hidden = new() { HideTaskbar = true };

    [Fact]
    public void The_bar_starts_at_the_top() => Assert.Equal(BarPosition.Top, new AppSettings().BarPosition);

    [Fact]
    public void Bottom_needs_the_taskbar_hidden()
    {
        var s = SettingsCatalog.Write(Hidden, "pos", "Bottom");
        Assert.Equal(BarPosition.Bottom, s.BarPosition);
        Assert.Equal("Bottom", SettingsCatalog.Read(s, "pos"));
    }

    [Fact]
    public void Showing_the_taskbar_again_moves_the_bar_to_the_top()
    {
        var bottom = SettingsCatalog.Write(Hidden, "pos", "Bottom");
        var s = SettingsCatalog.Write(bottom, "hideTb", false);
        Assert.Equal(BarPosition.Top, s.BarPosition);
        Assert.True(s.MovedToTop);
        Assert.Equal(new RowState(false, "Moved to the top because the Windows taskbar is back"), SettingsCatalog.State(s, "pos"));
    }

    [Fact]
    public void A_bottom_bar_in_settings_json_without_a_hidden_taskbar_reads_as_top()
    {
        var s = new AppSettings { BarPosition = BarPosition.Bottom }.Normalize();
        Assert.Equal(BarPosition.Top, s.BarPosition);
    }

    [Fact]
    public void Position_is_greyed_out_while_the_taskbar_shows()
    {
        Assert.Equal(new RowState(false, "Hide the Windows taskbar first"), SettingsCatalog.State(new AppSettings(), "pos"));
        Assert.Equal(new RowState(true, null), SettingsCatalog.State(Hidden, "pos"));
    }

    [Fact]
    public void Hiding_the_taskbar_again_forgets_the_move_note()
    {
        var moved = SettingsCatalog.Write(SettingsCatalog.Write(SettingsCatalog.Write(Hidden, "pos", "Bottom"), "hideTb", false), "hideTb", true);
        Assert.False(moved.MovedToTop);
        Assert.Equal(BarPosition.Top, moved.BarPosition);
    }

    [Fact]
    public void Picking_a_position_forgets_the_move_note()
    {
        var moved = Hidden with { MovedToTop = true };
        Assert.False(SettingsCatalog.Write(moved, "pos", "Top").MovedToTop);
    }

    [Fact]
    public void Position_row_is_available() =>
        Assert.True(SettingsCatalog.Sections.SelectMany(s => s.Rows).Single(r => r.Key == "pos").Available);

    [Theory]
    [InlineData(false, 4, 24, 32, 4)]
    [InlineData(true, 4, 24, 32, 4)]
    [InlineData(true, 4, 300, 600, 296)]
    [InlineData(true, 10, 24, 600, 566)]
    public void Heights_from_the_top_mirror_to_the_bottom(bool bottom, double y, double height, double envelope, double expected) =>
        Assert.Equal(expected, IslandLayout.FromEdge(y, height, envelope, bottom));
}
