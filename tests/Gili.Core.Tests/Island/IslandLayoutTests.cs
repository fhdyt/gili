using Gili.Core.Island;

namespace Gili.Core.Tests.Island;

public class IslandLayoutTests
{
    private static IslandGeometry Layout(
        IslandMode mode, double content = 120, bool hovered = false, IslandPage page = IslandPage.Media, double screen = 1536) =>
        IslandLayout.For(mode, page, content, hovered, barHeight: 32, islandHeight: 24, screenWidth: screen);

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
    public void Expanded_floats_8_below_the_top() =>
        Assert.Equal(new IslandGeometry(680, 256, 8, 32), Layout(IslandMode.Expanded));

    [Fact]
    public void The_notes_page_makes_the_island_larger() =>
        Assert.Equal(new IslandGeometry(960, 500, 8, 32), Layout(IslandMode.Expanded, page: IslandPage.Notes));

    [Fact]
    public void Cards_and_the_drop_zone_have_their_own_size()
    {
        Assert.Equal(new IslandGeometry(560, 104, 8, 28), Layout(IslandMode.Card));
        Assert.Equal(new IslandGeometry(420, 140, 8, 30), Layout(IslandMode.DropZone));
    }

    [Fact]
    public void A_floating_island_leaves_24_of_the_screen() =>
        Assert.Equal(576, Layout(IslandMode.Expanded, screen: 600).Width);

    [Theory]
    [InlineData(1110, 1150, true)]
    [InlineData(1117, 1150, false)]
    [InlineData(380, 421, true)]
    [InlineData(380, 419, false)]
    [InlineData(600, 700, true)]
    public void An_element_is_covered_within_8_of_the_island(double left, double right, bool covered) =>
        Assert.Equal(covered, IslandLayout.Covers(428, 1108, left, right));
}
