using Gili.Core.Island;

namespace Gili.Core.Tests.Island;

public class IslandLayoutTests
{
    private static IslandGeometry Layout(
        IslandMode mode, double content = 120, bool hovered = false, double screen = 1920, double expandedWidth = 560, double pageHeight = 112) =>
        IslandLayout.For(mode, new IslandScreen(32, 24, screen, 1080), content, hovered, expandedWidth, pageHeight);

    [Fact]
    public void Compact_is_never_narrower_than_200() => Assert.Equal(200, Layout(IslandMode.Compact, content: 100).Width);

    [Fact]
    public void Compact_fits_its_content_plus_28() => Assert.Equal(328, Layout(IslandMode.Compact, content: 300).Width);

    [Fact]
    public void Hovering_compact_adds_16() => Assert.Equal(216, Layout(IslandMode.Compact, content: 100, hovered: true).Width);

    [Fact]
    public void Compact_is_centred_in_the_bar_with_round_ends() =>
        Assert.Equal(new IslandGeometry(200, 24, 4, 12), Layout(IslandMode.Compact));

    [Fact]
    public void Wide_adds_32_and_ignores_hover()
    {
        Assert.Equal(232, Layout(IslandMode.Wide, content: 200, hovered: true).Width);
        Assert.Equal(200, Layout(IslandMode.Wide, content: 100).Width);
    }

    [Fact]
    public void Floating_states_keep_the_compact_top_edge()
    {
        Assert.Equal(new IslandGeometry(560, 256, 4, 32), Layout(IslandMode.Expanded));
        Assert.Equal(new IslandGeometry(560, 104, 4, 28), Layout(IslandMode.Card));
        Assert.Equal(new IslandGeometry(420, 140, 4, 30), Layout(IslandMode.DropZone));
    }

    [Fact]
    public void Expanded_is_as_tall_as_its_page() =>
        Assert.Equal(58 + 300 + 20, Layout(IslandMode.Expanded, pageHeight: 300).Height);

    [Fact]
    public void Expanded_stops_at_the_screen_height_less_the_top_gap_twice() =>
        Assert.Equal(1080 - 8, Layout(IslandMode.Expanded, pageHeight: 2000).Height);

    [Fact]
    public void Expanded_takes_the_menu_width() => Assert.Equal(578, Layout(IslandMode.Expanded, expandedWidth: 578).Width);

    [Fact]
    public void A_card_leaves_24_of_the_screen() => Assert.Equal(476, Layout(IslandMode.Card, screen: 500).Width);

    [Theory]
    [InlineData(1110, 1150, true)]
    [InlineData(1117, 1150, false)]
    [InlineData(380, 421, true)]
    [InlineData(380, 419, false)]
    [InlineData(600, 700, true)]
    public void An_element_is_covered_within_8_of_the_island(double left, double right, bool covered) =>
        Assert.Equal(covered, IslandLayout.Covers(428, 1108, left, right));
}
