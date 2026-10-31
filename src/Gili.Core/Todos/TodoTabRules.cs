using Gili.Core.Layout;

namespace Gili.Core.Todos;

/// <summary>What dragging a row or pressing Alt + arrow does in the Todo tab (F-TOOL-06, mockup tdMove).</summary>
public static class TodoTabRules
{
    /// <summary>Rows can be moved in Today and All; Done is a record of finished tasks.</summary>
    public static bool CanReorder(TodoFilter filter) => filter != TodoFilter.Done;

    /// <summary>
    /// The full order after <paramref name="id"/> is dropped on <paramref name="target"/>: it takes the place of the target in
    /// the whole list, so tasks hidden by the filter keep theirs. Null when nothing would change.
    /// </summary>
    public static IReadOnlyList<long>? Drop(IReadOnlyList<Todo> todos, long id, long target)
    {
        var full = Order(todos);
        var to = full.IndexOf(target);
        if (to < 0 || id == target) return null;
        var moved = ListOrder.Move(full, id, to);
        return ReferenceEquals(moved, full) ? null : moved;
    }

    /// <summary>
    /// Alt + arrow on a row: it trades places with its neighbour among the visible rows (up is -1). Null at either end,
    /// when the filter is Done, or when the row is not shown.
    /// </summary>
    public static IReadOnlyList<long>? Step(IReadOnlyList<Todo> todos, IReadOnlyList<long> visible, TodoFilter filter, long id, int delta) =>
        CanReorder(filter) && ListOrder.Neighbor(visible, id, delta) is { } neighbor ? Drop(todos, id, neighbor) : null;

    private static List<long> Order(IReadOnlyList<Todo> todos) =>
        [.. todos.OrderBy(t => t.SortOrder).ThenBy(t => t.Id).Select(t => t.Id)];
}
