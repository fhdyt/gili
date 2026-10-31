using Gili.Core.Notes;

namespace Gili.Core.Tests.Notes;

public sealed class NoteFloatTests
{
    // A 1920 × 1080 screen with a 40 px bar on top and a 48 px taskbar below.
    private static readonly NoteRect Work = new(0, 40, 1920, 992);

    [Fact]
    public void Float_on_top_starts_at_the_top_right_and_steps_in_24_px_each()
    {
        Assert.Equal(new NoteRect(1920 - 260 - 24, 40 + 24, 260, 220), NoteFloat.Default(0, Work));
        Assert.Equal(new NoteRect(1920 - 260 - 48, 40 + 48, 260, 220), NoteFloat.Default(1, Work));
    }

    [Fact]
    public void Six_notes_float_at_most()
    {
        Assert.True(NoteFloat.CanFloat(5));
        Assert.False(NoteFloat.CanFloat(6));
    }

    [Theory]
    [InlineData(-30, 500, 0, 500)]
    [InlineData(1800, 500, 1660, 500)]
    [InlineData(300, 10, 300, 40)]
    [InlineData(300, 1000, 300, 812)]
    public void Clamp_keeps_the_note_inside_the_work_area(double x, double y, double ex, double ey)
    {
        Assert.Equal(new NoteRect(ex, ey, 260, 220), NoteFloat.Clamp(new NoteRect(x, y, 260, 220), Work));
    }

    [Fact]
    public void Clamp_shrinks_a_note_bigger_than_its_limits_or_the_work_area()
    {
        Assert.Equal(new NoteRect(0, 40, 520, 640), NoteFloat.Clamp(new NoteRect(0, 40, 900, 900), Work));
        var small = new NoteRect(0, 0, 400, 300);
        Assert.Equal(new NoteRect(0, 0, 400, 300), NoteFloat.Clamp(new NoteRect(0, 0, 500, 500), small));
        Assert.Equal(new NoteRect(0, 40, 200, 140), NoteFloat.Clamp(new NoteRect(0, 40, 50, 50), Work));
    }

    [Fact]
    public void Released_keeps_the_held_point_where_it_was_let_go()
    {
        // Held at 25% across and 50% down; released at (1000, 600).
        Assert.Equal(new NoteRect(1000 - 65, 600 - 110, 260, 220), NoteFloat.Released(1000, 600, 0.25, 0.5));
    }

    [Theory]
    [InlineData(3, 100, ResizeEdges.Left)]
    [InlineData(257, 100, ResizeEdges.Right)]
    [InlineData(100, 2, ResizeEdges.Top)]
    [InlineData(100, 216, ResizeEdges.Bottom)]
    [InlineData(10, 10, ResizeEdges.Left | ResizeEdges.Top)]
    [InlineData(250, 210, ResizeEdges.Right | ResizeEdges.Bottom)]
    [InlineData(250, 5, ResizeEdges.Right | ResizeEdges.Top)]
    [InlineData(5, 215, ResizeEdges.Left | ResizeEdges.Bottom)]
    [InlineData(100, 100, ResizeEdges.None)]
    [InlineData(8, 100, ResizeEdges.None)]
    public void Edges_are_6_px_bands_and_corners_12_px_squares(double x, double y, ResizeEdges expected)
    {
        Assert.Equal(expected, NoteFloat.HitEdge(x, y, 260, 220));
    }

    [Fact]
    public void Resizing_keeps_the_opposite_edge_in_place()
    {
        var start = new NoteRect(500, 300, 260, 220);
        Assert.Equal(new NoteRect(500, 300, 300, 250), NoteFloat.Resize(start, ResizeEdges.Right | ResizeEdges.Bottom, 40, 30, Work));
        Assert.Equal(new NoteRect(460, 270, 300, 250), NoteFloat.Resize(start, ResizeEdges.Left | ResizeEdges.Top, -40, -30, Work));
        Assert.Equal(new NoteRect(500, 300, 260, 220), NoteFloat.Resize(start, ResizeEdges.None, 40, 30, Work));
    }

    [Fact]
    public void Resizing_stops_at_the_limits()
    {
        var start = new NoteRect(500, 300, 260, 220);
        Assert.Equal(new NoteRect(500, 300, 200, 140), NoteFloat.Resize(start, ResizeEdges.Right | ResizeEdges.Bottom, -200, -200, Work));
        Assert.Equal(new NoteRect(560, 380, 200, 140), NoteFloat.Resize(start, ResizeEdges.Left | ResizeEdges.Top, 200, 200, Work));
        Assert.Equal(new NoteRect(500, 300, 520, 640), NoteFloat.Resize(start, ResizeEdges.Right | ResizeEdges.Bottom, 900, 900, Work));
    }

    [Fact]
    public void Resizing_never_leaves_the_work_area()
    {
        var corner = new NoteRect(1600, 60, 260, 220);
        // The right edge stops at the screen's edge, the top at the bar.
        Assert.Equal(new NoteRect(1600, 40, 320, 240), NoteFloat.Resize(corner, ResizeEdges.Right | ResizeEdges.Top, 200, -200, Work));
        var low = new NoteRect(10, 800, 260, 220);
        Assert.Equal(new NoteRect(0, 800, 270, 232), NoteFloat.Resize(low, ResizeEdges.Left | ResizeEdges.Bottom, -60, 60, Work));
    }

    [Theory]
    [InlineData(16, 0, 16)]
    [InlineData(16, 0.5, 14)]
    [InlineData(16, 1, 12)]
    [InlineData(12, 0.5, 12)]
    [InlineData(16, 1.4, 12)]
    public void Radius_goes_to_12_with_the_size(double from, double progress, double expected)
    {
        Assert.Equal(expected, NoteFloat.Radius(from, progress), 6);
    }
}
