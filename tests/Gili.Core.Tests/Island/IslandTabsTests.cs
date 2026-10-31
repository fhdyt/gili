using Gili.Core.Island;
using Gili.Core.Settings;

namespace Gili.Core.Tests.Island;

public class IslandTabsTests
{
    [Fact]
    public void The_menu_is_the_ten_pages_without_controls()
    {
        Assert.Equal(10, IslandTabs.Menu.Count);
        Assert.Equal(IslandPage.Media, IslandTabs.Menu[0]);
        Assert.DoesNotContain(IslandPage.Controls, IslandTabs.Menu);
        Assert.Equal(PageSet.Notes, IslandTabs.Of(IslandPage.Notes));
    }

    [Fact]
    public void Hidden_tabs_leave_the_menu()
    {
        var visible = IslandTabs.Visible(PageSet.Timer | PageSet.Shelf, temporary: null);
        Assert.Equal(8, visible.Count);
        Assert.DoesNotContain(IslandPage.Timer, visible);
        Assert.DoesNotContain(IslandPage.Shelf, visible);
    }

    [Fact]
    public void Temporary_tab_returns_at_its_place()
    {
        var visible = IslandTabs.Visible(PageSet.Timer, temporary: IslandPage.Timer);
        Assert.Equal(IslandPage.Timer, visible[1]);
        Assert.Equal(10, visible.Count);
    }

    [Fact]
    public void Arrows_skip_hidden_tabs()
    {
        var visible = IslandTabs.Visible(PageSet.Timer, null);
        Assert.Equal(IslandPage.Alarms, IslandTabs.Step(visible, IslandPage.Media, 1));
        Assert.Equal(IslandPage.Media, IslandTabs.Step(visible, IslandPage.Alarms, -1));
        Assert.Equal(IslandPage.System, IslandTabs.Step(visible, IslandPage.System, 1));
        Assert.Equal(IslandPage.Media, IslandTabs.Step(visible, IslandPage.Media, -1));
    }

    [Fact]
    public void A_page_outside_the_menu_stays_put() =>
        Assert.Equal(IslandPage.Controls, IslandTabs.Step(IslandTabs.Visible(PageSet.None, null), IslandPage.Controls, 1));

    [Fact]
    public void Fallback_prefers_the_left_neighbour()
    {
        Assert.Equal(IslandPage.Alarms, IslandTabs.Fallback(PageSet.Calendar, IslandPage.Calendar));
        Assert.Equal(IslandPage.Media, IslandTabs.Fallback(PageSet.Timer | PageSet.Alarms | PageSet.Calendar, IslandPage.Alarms));
        Assert.Equal(IslandPage.Timer, IslandTabs.Fallback(PageSet.Media, IslandPage.Media));
    }
}
