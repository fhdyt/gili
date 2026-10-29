namespace Gili.Core.Notes;

/// <summary>
/// Dragging a card to reorder (PRD 6.3 "Mengatur urutan", mockup pointermove and dropBack): it lifts once the pointer
/// has moved 6 px, takes the place of the card whose resting slot is under the pointer, and lands in its own slot.
/// </summary>
public static class NoteDrag
{
    public const double LiftDistance = 6;

    /// <summary>After each swap the cards slide for a moment before the next one (mockup lock).</summary>
    public static readonly TimeSpan SwapPause = TimeSpan.FromMilliseconds(260);

    public static bool Lifts(double dx, double dy) => dx * dx + dy * dy >= LiftDistance * LiftDistance;

    /// <summary>
    /// The order once the pointer (in the grid's coordinates) is over another card's slot: the dragged note takes that
    /// card's place. The same list when it is over its own slot, a gap, or nothing.
    /// </summary>
    public static IReadOnlyList<long> Reorder(IReadOnlyList<long> order, IReadOnlyList<NoteRect> slots, long id, double x, double y)
    {
        if (slots.Count != order.Count) return order;
        var from = -1;
        for (var i = 0; i < order.Count; i++)
            if (order[i] == id) from = i;
        if (from < 0 || NoteGrid.HitTest(slots, x, y, from) is not { } over) return order;
        return NoteOrder.Move(order, id, over);
    }

    /// <summary>Where a released card lands: its slot, moved into the visible grid when it is scrolled out of view.</summary>
    public static NoteRect Landing(NoteRect slot, NoteRect visible)
    {
        var x = Math.Clamp(slot.X, visible.X, Math.Max(visible.X, visible.Right - slot.Width));
        var y = Math.Clamp(slot.Y, visible.Y, Math.Max(visible.Y, visible.Bottom - slot.Height));
        return slot with { X = x, Y = y };
    }
}
