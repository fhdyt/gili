using Gili.Core.Apps;

namespace Gili.Core.Tests.Apps;

public class AppStripTests
{
    private readonly AppStrip _strip = new();

    private static TaskbarWindow W(
        nint handle = 1, string app = "chrome", bool visible = true, bool tool = false, bool owner = false,
        bool appWindow = false, bool cloaked = false, bool own = false) =>
        new(handle, app, visible, tool, owner, appWindow, cloaked, own);

    [Fact]
    public void Tool_owned_cloaked_hidden_and_own_windows_stay_off_the_strip()
    {
        Assert.True(TaskbarFilter.Shows(W()));
        Assert.False(TaskbarFilter.Shows(W(tool: true)));
        Assert.False(TaskbarFilter.Shows(W(owner: true)));
        Assert.True(TaskbarFilter.Shows(W(owner: true, appWindow: true)));
        Assert.False(TaskbarFilter.Shows(W(cloaked: true)));
        Assert.False(TaskbarFilter.Shows(W(own: true)));
        Assert.False(TaskbarFilter.Shows(W(visible: false)));
    }

    [Fact]
    public void Apps_keep_the_order_they_were_first_opened()
    {
        _strip.Update([W(1, "chrome"), W(2, "code")]);
        _strip.Update([W(3, "spotify"), W(1, "chrome"), W(2, "code")]);
        Assert.Equal(["chrome", "code", "spotify"], _strip.Apps);
    }

    [Fact]
    public void Two_windows_of_one_app_are_one_icon()
    {
        _strip.Update([W(1, "chrome"), W(2, "chrome")]);
        Assert.Equal(["chrome"], _strip.Apps);
    }

    [Fact]
    public void Closing_one_of_two_windows_keeps_the_app()
    {
        _strip.Update([W(1, "chrome"), W(2, "chrome")]);
        Assert.False(_strip.Update([W(2, "chrome")]));
        Assert.Equal(["chrome"], _strip.Apps);
    }

    [Fact]
    public void Closing_the_last_window_removes_the_app()
    {
        _strip.Update([W(1, "chrome"), W(2, "code")]);
        Assert.True(_strip.Update([W(2, "code")]));
        Assert.Equal(["code"], _strip.Apps);
    }

    [Fact]
    public void Windows_the_taskbar_would_not_show_do_not_add_apps()
    {
        _strip.Update([W(1, "chrome"), W(2, "tooltip", tool: true)]);
        Assert.Equal(["chrome"], _strip.Apps);
    }

    [Fact]
    public void Only_listed_apps_become_active()
    {
        _strip.Update([W(1, "chrome")]);
        Assert.False(_strip.SetActive("desktop"));
        Assert.True(_strip.SetActive("chrome"));
        Assert.Equal("chrome", _strip.Active);
    }

    [Fact]
    public void The_active_app_is_dropped_when_it_closes()
    {
        _strip.Update([W(1, "chrome"), W(2, "code")]);
        _strip.SetActive("chrome");
        _strip.Update([W(2, "code")]);
        Assert.Null(_strip.Active);
    }

    [Fact]
    public void Everything_fits_on_a_wide_bar() => Assert.Equal(new StripFit(0, 160, 160, false), AppStrip.Fit(600, 5, 0, 50));

    [Fact]
    public void Inactive_icons_drop_into_more_from_the_right() => Assert.Equal(3, AppStrip.Fit(200, 6, 0, 50).Hidden);

    [Fact]
    public void Active_app_never_goes_into_more() => Assert.Equal(4, AppStrip.Fit(60, 5, 4, 50).Hidden);

    [Fact]
    public void Little_room_left_makes_hover_names_tooltips()
    {
        var fit = AppStrip.Fit(200, 4, 0, 60);
        Assert.Equal(80, fit.ActiveNameMax);
        Assert.True(fit.Tight);
    }

    [Fact]
    public void Left_side_never_comes_closer_than_150_plus_the_gap() => Assert.Equal(768 - 150 - 24 - 10, BarSpacing.LeftLimit(1536, 200, 24, giliCircle: false, privacyDots: 0));

    [Fact]
    public void Left_limit_keeps_24_from_the_hovered_island() => Assert.Equal(768 - 158 - 24 - 10, BarSpacing.LeftLimit(1536, 300, 24, giliCircle: false, privacyDots: 0));

    [Fact]
    public void Gili_circle_and_privacy_dots_push_the_left_side_away() =>
        Assert.Equal(768 - (158 + 30 + 13) - 24 - 10, BarSpacing.LeftLimit(1536, 300, 24, giliCircle: true, privacyDots: 1));

    [Fact]
    public void Both_privacy_dots_push_the_left_side_away() =>
        Assert.Equal(768 - (158 + 30 + 26) - 24 - 10, BarSpacing.LeftLimit(1536, 300, 24, giliCircle: true, privacyDots: 2));

    [Fact]
    public void Bubbles_push_the_right_side_away() => Assert.Equal(768 - (158 + 30) - 24 - 10, BarSpacing.RightLimit(1536, 300, 24, bubbles: 1));
}
