namespace Gili.Core.Notes;

/// <summary>Reordering the notes by drag or by Alt + arrow key.</summary>
public static class NoteOrder
{
    /// <summary>The order with <paramref name="id"/> moved to <paramref name="toIndex"/>; the same list when nothing would change.</summary>
    public static IReadOnlyList<long> Move(IReadOnlyList<long> order, long id, int toIndex)
    {
        var from = IndexOf(order, id);
        if (from < 0 || toIndex < 0 || toIndex >= order.Count || toIndex == from) return order;
        var moved = order.ToList();
        moved.RemoveAt(from);
        moved.Insert(toIndex, id);
        return moved;
    }

    /// <summary>The note next to <paramref name="id"/> in the visible order (left/up is -1); null at either end or when id is not shown.</summary>
    public static long? Neighbor(IReadOnlyList<long> visible, long id, int delta)
    {
        var from = IndexOf(visible, id);
        if (from < 0) return null;
        var to = from + delta;
        return to < 0 || to >= visible.Count ? null : visible[to];
    }

    private static int IndexOf(IReadOnlyList<long> list, long id)
    {
        for (var i = 0; i < list.Count; i++)
            if (list[i] == id) return i;
        return -1;
    }
}
