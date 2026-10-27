using Gili.Core.Notes;

namespace Gili.Core.Tests.Notes;

public class NoteStyleTests
{
    [Fact]
    public void Size_steps_stop_at_the_ends()
    {
        Assert.Equal(NoteTextSize.Large, NoteStyle.Step(NoteTextSize.Large, 1));
        Assert.Equal(NoteTextSize.ExtraSmall, NoteStyle.Step(NoteTextSize.ExtraSmall, -1));
        Assert.Equal(NoteTextSize.Large, NoteStyle.Step(NoteTextSize.Medium, 1));
        Assert.Equal(NoteTextSize.Small, NoteStyle.Step(NoteTextSize.Medium, -1));
    }

    [Fact]
    public void Size_table_matches_the_prd()
    {
        var medium = NoteStyle.Size(NoteTextSize.Medium);
        Assert.Equal("Medium", medium.Name);
        Assert.Equal((14d, 13d, 13d, 12d, 13d), (medium.Editor, medium.Floating, medium.CardTitle, medium.CardBody, medium.Glyph));
        var large = NoteStyle.Size(NoteTextSize.Large);
        Assert.Equal((16d, 15d, 14.5, 13.5, 15d), (large.Editor, large.Floating, large.CardTitle, large.CardBody, large.Glyph));
        var xs = NoteStyle.Size(NoteTextSize.ExtraSmall);
        Assert.Equal("Extra small", xs.Name);
        Assert.Equal((12d, 11d, 12d, 11d, 10d), (xs.Editor, xs.Floating, xs.CardTitle, xs.CardBody, xs.Glyph));
        var small = NoteStyle.Size(NoteTextSize.Small);
        Assert.Equal((13d, 12d, 12.5, 11.5, 11.5), (small.Editor, small.Floating, small.CardTitle, small.CardBody, small.Glyph));
        Assert.Equal(NoteTextSize.Medium, NoteStyle.Default);
    }

    [Fact]
    public void Colours_follow_the_prd_order()
    {
        Assert.Equal(8, NoteStyle.Colors.Count);
        Assert.Equal(new NoteColor("Default", 0x1C1C1E), NoteStyle.Colors[0]);
        Assert.Equal(new NoteColor("Red", 0x5C1F26), NoteStyle.Colors[1]);
        Assert.Equal(new NoteColor("Purple", 0x42265A), NoteStyle.Colors[7]);
    }

    [Fact]
    public void Unknown_color_is_default()
    {
        Assert.Equal(0, NoteStyle.ClampColor(9));
        Assert.Equal(0, NoteStyle.ClampColor(-1));
        Assert.Equal(7, NoteStyle.ClampColor(7));
        Assert.Equal(3, NoteStyle.ClampColor(3));
    }
}
