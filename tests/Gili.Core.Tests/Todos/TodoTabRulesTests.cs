using Gili.Core.Todos;

namespace Gili.Core.Tests.Todos;

public class TodoTabRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 31);

    private static Todo Task(long id, DateOnly? due = null) =>
        new(id, $"T{id}", due, null, null, null, (int)id, DateTimeOffset.UnixEpoch);

    // 1 today, 2 tomorrow (hidden in Today), 3 undated, 4 today.
    private static readonly Todo[] All = [Task(1, Today), Task(2, Today.AddDays(1)), Task(3), Task(4, Today)];

    private static IReadOnlyList<long> Visible(TodoFilter filter) => [.. TodoSchedule.Filter(All, filter, Today).Select(t => t.Id)];

    [Fact]
    public void Only_Done_refuses_to_reorder()
    {
        Assert.True(TodoTabRules.CanReorder(TodoFilter.Today));
        Assert.True(TodoTabRules.CanReorder(TodoFilter.All));
        Assert.False(TodoTabRules.CanReorder(TodoFilter.Done));
    }

    [Fact]
    public void A_drop_in_Today_takes_the_place_of_the_target_in_the_full_order()
    {
        // Task 1 dropped on task 3 skips over the hidden task 2 in the visible list but takes 3's place overall.
        Assert.Equal([2L, 3L, 1L, 4L], TodoTabRules.Drop(All, 1, 3));
        Assert.Equal([4L, 1L, 2L, 3L], TodoTabRules.Drop(All, 4, 1));
    }

    [Fact]
    public void A_drop_on_itself_or_on_nothing_changes_nothing()
    {
        Assert.Null(TodoTabRules.Drop(All, 1, 1));
        Assert.Null(TodoTabRules.Drop(All, 1, 99));
        Assert.Null(TodoTabRules.Drop(All, 99, 1));
    }

    [Fact]
    public void Alt_arrow_trades_places_with_the_visible_neighbour_and_leaves_hidden_tasks_where_they_are()
    {
        Assert.Equal([1L, 2L, 4L, 3L], TodoTabRules.Step(All, Visible(TodoFilter.Today), TodoFilter.Today, 4, -1));
        Assert.Equal([2L, 1L, 3L, 4L], TodoTabRules.Step(All, Visible(TodoFilter.All), TodoFilter.All, 1, 1));
    }

    [Fact]
    public void Alt_arrow_does_nothing_at_the_ends_or_in_Done()
    {
        Assert.Null(TodoTabRules.Step(All, Visible(TodoFilter.Today), TodoFilter.Today, 1, -1));
        Assert.Null(TodoTabRules.Step(All, Visible(TodoFilter.Today), TodoFilter.Today, 4, 1));
        Assert.Null(TodoTabRules.Step(All, Visible(TodoFilter.All), TodoFilter.Done, 2, 1));
        Assert.Null(TodoTabRules.Step(All, Visible(TodoFilter.Today), TodoFilter.Today, 2, 1));
    }
}
