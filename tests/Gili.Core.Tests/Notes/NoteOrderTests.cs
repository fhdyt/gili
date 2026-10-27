using Gili.Core.Notes;

namespace Gili.Core.Tests.Notes;

public class NoteOrderTests
{
    [Fact]
    public void Moving_to_an_index_reorders()
    {
        Assert.Equal([2L, 3L, 1L], NoteOrder.Move([1L, 2L, 3L], 1, 2));
        Assert.Equal([3L, 1L, 2L], NoteOrder.Move([1L, 2L, 3L], 3, 0));
    }

    [Fact]
    public void Moving_past_either_end_changes_nothing()
    {
        long[] order = [1, 2, 3];
        Assert.Null(NoteOrder.Neighbor(order, 1, -1));
        Assert.Null(NoteOrder.Neighbor(order, 3, 1));
        Assert.Equal(3, NoteOrder.Neighbor(order, 2, 1));
        Assert.Equal(1, NoteOrder.Neighbor(order, 2, -1));
        Assert.Null(NoteOrder.Neighbor(order, 9, 1));
        Assert.Same(order, NoteOrder.Move(order, 2, 1));
        Assert.Same(order, NoteOrder.Move(order, 2, 5));
        Assert.Same(order, NoteOrder.Move(order, 2, -1));
        Assert.Same(order, NoteOrder.Move(order, 9, 0));
    }
}
