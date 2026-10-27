using Gili.Core.Notes;

namespace Gili.Core.Tests.Notes;

public class NoteGridTests
{
    [Fact]
    public void Columns_follow_the_width()
    {
        Assert.Equal(3, NoteGrid.Columns(512));
        Assert.Equal(1, NoteGrid.Columns(287));
        Assert.Equal(2, NoteGrid.Columns(288));
        Assert.Equal(1, NoteGrid.Columns(0));
        Assert.Equal(165.333, NoteGrid.ColumnWidth(512), 3);
    }

    [Fact]
    public void Cards_go_to_the_shortest_column()
    {
        var layout = NoteGrid.Place([100, 40, 40, 30], 512);
        Assert.Equal(4, layout.Slots.Count);
        Assert.Equal(0, layout.Slots[0].X);
        Assert.Equal(173.333, layout.Slots[3].X, 3);
        Assert.Equal(48, layout.Slots[3].Y);
        Assert.Equal(30, layout.Slots[3].Height);
        Assert.Equal(100, layout.Height);
    }

    [Fact]
    public void An_empty_list_has_no_height()
    {
        var layout = NoteGrid.Place([], 512);
        Assert.Empty(layout.Slots);
        Assert.Equal(0, layout.Height);
    }

    [Fact]
    public void Hit_test_skips_the_dragged_card()
    {
        var slots = new[] { new NoteRect(0, 0, 100, 50), new NoteRect(108, 0, 100, 50) };
        Assert.Equal(1, NoteGrid.HitTest(slots, 120, 10, except: 0));
        Assert.Null(NoteGrid.HitTest(slots, 10, 10, except: 0));
        Assert.Equal(0, NoteGrid.HitTest(slots, 10, 10, except: -1));
        Assert.Null(NoteGrid.HitTest(slots, 104, 10, except: -1));
    }

    [Fact]
    public void Editor_grows_toward_the_free_side()
    {
        var grid = new NoteRect(0, 0, 512, 600);
        var left = NoteGrid.Editor(new NoteRect(0, 0, 165, 80), grid);
        Assert.Equal(GrowX.Left, left.X);
        Assert.Equal(0, left.Frame.X);
        Assert.Equal(440, left.Frame.Width);
        Assert.Equal(360, left.Frame.Height);
        var center = NoteGrid.Editor(new NoteRect(173, 0, 165, 80), grid);
        Assert.Equal(GrowX.Center, center.X);
        Assert.Equal(35.5, center.Frame.X);
        var right = NoteGrid.Editor(new NoteRect(346, 0, 165, 80), grid);
        Assert.Equal(GrowX.Right, right.X);
        Assert.Equal(511, right.Frame.X + 440);
        Assert.Equal(GrowY.Down, right.Y);
    }

    [Fact]
    public void Editor_stays_inside_the_visible_grid()
    {
        var tall = NoteGrid.Editor(new NoteRect(0, 250, 165, 50), new NoteRect(0, 0, 512, 300));
        Assert.Equal(300, tall.Frame.Height);
        Assert.Equal(0, tall.Frame.Y);
        Assert.Equal(GrowY.Up, tall.Y);
        var narrow = NoteGrid.Editor(new NoteRect(0, 0, 165, 50), new NoteRect(0, 0, 400, 600));
        Assert.Equal(400, narrow.Frame.Width);
        Assert.Equal(GrowY.Down, narrow.Y);
    }

    [Fact]
    public void A_new_card_starts_at_the_top_right()
    {
        var card = NoteGrid.NewCard(new NoteRect(10, 20, 512, 300), 165);
        Assert.Equal(new NoteRect(357, 20, 165, 64), card);
    }
}
