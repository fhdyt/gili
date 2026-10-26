using Gili.Core.Island;

namespace Gili.Core.Tests.Island;

public class TabMenuLayoutTests
{
    private static readonly TabName[] Three = [new(30, 40), new(30, 40), new(30, 40)];

    [Fact]
    public void Nine_tabs_fit_in_560() => Assert.Equal(532, TabMenuLayout.Need(9, showGili: true, longestBold: 50));

    [Fact]
    public void Ten_tabs_widen_the_island() => Assert.Equal(578, TabMenuLayout.IslandWidth(TabMenuLayout.Need(10, true, 54), 1920));

    [Fact]
    public void Without_gili_the_circle_is_not_counted() => Assert.Equal(490, TabMenuLayout.Need(9, showGili: false, longestBold: 50));

    [Fact]
    public void Island_never_narrower_than_560() => Assert.Equal(560, TabMenuLayout.IslandWidth(400, 1920));

    [Fact]
    public void Narrow_screen_caps_the_width_and_goes_tight()
    {
        var width = TabMenuLayout.IslandWidth(578, 600);
        Assert.Equal(576, width);
        Assert.True(TabMenuLayout.IsTight(578, width));
        Assert.False(TabMenuLayout.IsTight(532, 560));
    }

    [Fact]
    public void Leftover_width_is_shared_evenly() =>
        Assert.Equal([95.333, 47.333, 47.333], TabMenuLayout.TabWidths(200, Three, active: 0, hovered: -1, tight: false).Select(x => Math.Round(x, 3)));

    [Fact]
    public void Short_menu_takes_back_in_proportion() =>
        Assert.Equal([75.385, 32.308, 32.308], TabMenuLayout.TabWidths(150, Three, active: 0, hovered: -1, tight: false).Select(x => Math.Round(x, 3)));

    [Fact]
    public void Hovered_tab_opens_with_the_regular_name_unless_tight()
    {
        var names = new TabName[] { new(30, 40), new(20, 24), new(30, 40) };
        // Menus exactly as wide as the tabs need, so no leftover is shared out.
        Assert.Equal([84, 64, 36], TabMenuLayout.TabWidths(194, names, active: 0, hovered: 1, tight: false));
        Assert.Equal([84, 36, 36], TabMenuLayout.TabWidths(166, names, active: 0, hovered: 1, tight: true));
    }

    [Fact]
    public void Highlight_sits_on_the_active_tab() =>
        Assert.Equal((3 + 95.5 + 2, 47.5), TabMenuLayout.Highlight([95.5, 47.5, 47.0], active: 1));
}
