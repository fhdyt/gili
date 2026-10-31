using Gili.Core.Devices;
using Gili.Core.Island;

namespace Gili.Core.Tests.Devices;

public class PrivacyTests
{
    [Theory]
    [InlineData(134303765027089966, 0, true)]
    [InlineData(134303765027089966, 134303765067155859, false)]
    [InlineData(0, 0, false)]
    public void In_use_needs_a_start_and_no_stop(long start, long stop, bool inUse) => Assert.Equal(inUse, PrivacyNames.IsInUse(start, stop));

    [Fact]
    public void Tooltip_lists_every_app()
    {
        Assert.Equal("Camera in use: Zoom", PrivacyText.Camera(["Zoom"]));
        Assert.Equal("Microphone in use: Discord, Chrome", PrivacyText.Microphone(["Discord", "Chrome"]));
    }

    [Fact]
    public void Exe_keys_become_paths() =>
        Assert.Equal(@"C:\Users\Lenovo\AppData\Roaming\Zoom\bin\Zoom.exe", PrivacyNames.ExePath("C:#Users#Lenovo#AppData#Roaming#Zoom#bin#Zoom.exe"));

    [Theory]
    [InlineData("Microsoft.Copilot_8wekyb3d8bbwe", true, "Copilot")]
    [InlineData("Contoso_abc", true, "Contoso")]
    [InlineData("NoUnderscore", true, "NoUnderscore")]
    [InlineData("C:#Users#Lenovo#AppData#Roaming#Zoom#bin#Zoom.exe", false, "Zoom")]
    [InlineData("NoHashes.exe", false, "NoHashes")]
    [InlineData("", false, "An app")]
    [InlineData("", true, "An app")]
    [InlineData("C:#Apps#", false, "An app")]
    public void Names_fall_back_for_odd_keys(string key, bool packaged, string name) => Assert.Equal(name, PrivacyNames.Fallback(key, packaged));

    [Fact]
    public void Long_names_are_cut_at_40() => Assert.Equal(40, PrivacyNames.Fallback(new string('a', 60) + ".exe", packaged: false).Length);

    [Fact]
    public void Use_counts_its_dots_and_compares_by_content()
    {
        var both = new PrivacyUse(["Zoom"], ["Discord"]);
        Assert.Equal(2, both.Count);
        Assert.True(both.Camera && both.Microphone);
        Assert.Equal(0, PrivacyUse.None.Count);
        Assert.Equal(new PrivacyUse(["Zoom"], ["Discord"]), both);
        Assert.NotEqual(new PrivacyUse(["Zoom"], []), both);
    }

    [Fact]
    public void Dots_line_up_left_of_the_island_camera_first() =>
        Assert.Equal([new DotPosition(846, 12), new DotPosition(833, 12)],
            IslandLayout.PrivacyDots(islandLeft: 860, islandTop: 4, islandHeight: 24, circleSpace: 0, floating: false, circleBesideFloating: false, count: 2));

    [Fact]
    public void Dots_make_room_for_gilis_circle() =>
        Assert.Equal(816, IslandLayout.PrivacyDots(860, 4, 24, circleSpace: 30, floating: false, circleBesideFloating: false, count: 1)[0].X);

    [Fact]
    public void Floating_dots_stack_down_the_panel_edge()
    {
        Assert.Equal([new DotPosition(846, 24), new DotPosition(846, 37)], IslandLayout.PrivacyDots(860, 4, 24, 30, floating: true, circleBesideFloating: false, count: 2));
        Assert.Equal(36, IslandLayout.PrivacyDots(860, 4, 24, 30, floating: true, circleBesideFloating: true, count: 1)[0].Y);
    }

    [Fact]
    public void No_dots_no_positions() => Assert.Empty(IslandLayout.PrivacyDots(860, 4, 24, 0, false, false, 0));
}
