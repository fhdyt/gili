using Gili.Core.Notes;

namespace Gili.Core.Tests.Notes;

public class NoteDragTests
{
    // Two columns of 140: 1 and 3 on the left, 2 on the right.
    private static readonly NoteRect[] Slots = [new(0, 0, 140, 60), new(148, 0, 140, 80), new(0, 68, 140, 60)];

    [Fact]
    public void A_press_lifts_after_six_pixels()
    {
        Assert.False(NoteDrag.Lifts(3, 4));
        Assert.False(NoteDrag.Lifts(0, 5.9));
        Assert.True(NoteDrag.Lifts(6, 0));
        Assert.True(NoteDrag.Lifts(-5, -5));
    }

    [Fact]
    public void Passing_over_another_card_takes_its_place()
    {
        long[] order = [1, 2, 3];
        Assert.Equal([2L, 3L, 1L], NoteDrag.Reorder(order, Slots, 1, 20, 100));
        Assert.Equal([1L, 3L, 2L], NoteDrag.Reorder(order, Slots, 3, 200, 10));
    }

    [Fact]
    public void Over_its_own_slot_or_a_gap_nothing_moves()
    {
        long[] order = [1, 2, 3];
        Assert.Same(order, NoteDrag.Reorder(order, Slots, 1, 20, 20));
        Assert.Same(order, NoteDrag.Reorder(order, Slots, 1, 144, 20));
        Assert.Same(order, NoteDrag.Reorder(order, Slots, 1, 500, 500));
        Assert.Same(order, NoteDrag.Reorder(order, Slots, 9, 200, 10));
        Assert.Same(order, NoteDrag.Reorder(order, Slots[..2], 1, 200, 10));
    }

    [Fact]
    public void A_visible_slot_lands_where_it_is()
    {
        var view = new NoteRect(0, 0, 440, 300);
        Assert.Equal(new NoteRect(148, 40, 140, 80), NoteDrag.Landing(new NoteRect(148, 40, 140, 80), view));
    }

    [Fact]
    public void A_slot_scrolled_out_of_view_lands_at_the_nearest_edge()
    {
        var view = new NoteRect(0, 0, 440, 300);
        Assert.Equal(new NoteRect(0, 0, 140, 60), NoteDrag.Landing(new NoteRect(0, -200, 140, 60), view));
        Assert.Equal(new NoteRect(148, 220, 140, 80), NoteDrag.Landing(new NoteRect(148, 900, 140, 80), view));
        // Taller than the view: its top stays at the top.
        Assert.Equal(new NoteRect(0, 0, 140, 400), NoteDrag.Landing(new NoteRect(0, 50, 140, 400), view));
    }
}
