using Gili.Core.Layout;

namespace Gili.Core.Tests.Layout;

public class ListOrderTests
{
    [Fact]
    public void Moving_to_an_index_reorders()
    {
        Assert.Equal([2L, 3L, 1L], ListOrder.Move([1L, 2L, 3L], 1, 2));
        Assert.Equal([3L, 1L, 2L], ListOrder.Move([1L, 2L, 3L], 3, 0));
    }

    [Fact]
    public void Moving_past_either_end_changes_nothing()
    {
        long[] order = [1, 2, 3];
        Assert.Null(ListOrder.Neighbor(order, 1, -1));
        Assert.Null(ListOrder.Neighbor(order, 3, 1));
        Assert.Equal(3, ListOrder.Neighbor(order, 2, 1));
        Assert.Equal(1, ListOrder.Neighbor(order, 2, -1));
        Assert.Null(ListOrder.Neighbor(order, 9, 1));
        Assert.Same(order, ListOrder.Move(order, 2, 1));
        Assert.Same(order, ListOrder.Move(order, 2, 5));
        Assert.Same(order, ListOrder.Move(order, 2, -1));
        Assert.Same(order, ListOrder.Move(order, 9, 0));
    }

    [Fact]
    public void Moving_in_a_filtered_view_keeps_hidden_items_in_place()
    {
        IReadOnlyList<long> full = [1, 2, 3, 4, 5];
        IReadOnlyList<long> visible = [1, 3, 5];
        Assert.Equal([1L, 2L, 5L, 3L, 4L], ListOrder.Move(full, 5, full.ToList().IndexOf(visible[1])));
        Assert.Equal([2L, 3L, 1L, 4L, 5L], ListOrder.Move(full, 1, full.ToList().IndexOf(visible[1])));
    }
}
