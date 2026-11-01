using Gili.Core.Projects;

namespace Gili.Core.Tests.Projects;

public class TaskDragTests
{
    private static readonly DateOnly Mon = new(2026, 11, 2), Fri = new(2026, 11, 6);

    [Theory]
    [InlineData(14, 0)]
    [InlineData(15, 1)]
    [InlineData(-15, -1)]
    [InlineData(61, 2)]
    public void Pixels_round_to_the_nearest_day(double pixels, int days) => Assert.Equal(days, TaskDrag.Days(pixels, 30));

    [Fact]
    public void Move_shifts_both_dates() => Assert.Equal((Mon.AddDays(2), Fri.AddDays(2)), TaskDrag.Apply(DragEdge.Move, Mon, Fri, 2));

    [Fact]
    public void Start_moves_alone_and_never_passes_the_end()
    {
        Assert.Equal((Mon.AddDays(-3), Fri), TaskDrag.Apply(DragEdge.Start, Mon, Fri, -3));
        Assert.Equal((Fri, Fri), TaskDrag.Apply(DragEdge.Start, Mon, Fri, 9));
    }

    [Fact]
    public void End_moves_alone_and_never_passes_the_start()
    {
        Assert.Equal((Mon, Fri.AddDays(1)), TaskDrag.Apply(DragEdge.End, Mon, Fri, 1));
        Assert.Equal((Mon, Mon), TaskDrag.Apply(DragEdge.End, Mon, Fri, -9));
    }
}

public class TimelineRangeTests
{
    private static readonly DateOnly Today = new(2026, 11, 1);

    private static ProjectTask Task(int from, int to) => new(1, 1, "T", Today.AddDays(from), Today.AddDays(to), 0, null, 0, default);

    [Fact]
    public void Empty_project_spans_around_today() =>
        Assert.Equal(new TimelineSpan(Today.AddDays(-35), Today.AddDays(60)), TimelineRange.Of([], Today));

    [Fact]
    public void Span_covers_the_tasks_and_today() =>
        Assert.Equal(new TimelineSpan(Today.AddDays(-45), Today.AddDays(80)), TimelineRange.Of([Task(-10, 2), Task(5, 20)], Today));

    [Fact]
    public void Days_count_both_ends() => Assert.Equal(96, TimelineRange.Of([], Today).Days);

    [Fact]
    public void X_is_the_day_column() => Assert.Equal(35 * 30, TimelineRange.X(TimelineRange.Of([], Today), Today, 30));

    [Fact]
    public void A_project_that_fits_is_centred()
    {
        ProjectTask[] tasks = [Task(0, 3)];
        var span = TimelineRange.Of(tasks, Today);
        // Days 0..3 are four columns, so their middle is two columns after today's.
        Assert.Equal((35 + 2) * 30 - 250, TimelineRange.Home(tasks, Today, span, 30, 500));
    }

    [Fact]
    public void A_wide_project_puts_today_a_third_in()
    {
        ProjectTask[] tasks = [Task(-9, 10)];
        var span = TimelineRange.Of(tasks, Today);
        Assert.Equal(44 * 30 - 565 / 3.0, TimelineRange.Home(tasks, Today, span, 30, 565), 3);
    }

    [Fact]
    public void Scroll_stays_inside_the_span()
    {
        var span = new TimelineSpan(Today, Today.AddDays(99));
        Assert.Equal(0, TimelineRange.Clamp(-40, span, 30, 500));
        Assert.Equal(100 * 30 - 500, TimelineRange.Clamp(99999, span, 30, 500));
        Assert.Equal(0, TimelineRange.Clamp(10, new TimelineSpan(Today, Today.AddDays(3)), 30, 500));
    }
}

public class ProjectTextTests
{
    private static readonly DateOnly Today = new(2026, 11, 1);

    private static ProjectTask Task(int from, int to, bool done = false) =>
        new(1, 1, "Timeline page", Today.AddDays(from), Today.AddDays(to), 0, done ? DateTimeOffset.UnixEpoch : null, 0, default);

    [Fact]
    public void Meta_counts_done_and_shows_the_span() =>
        Assert.Equal("1/2 done · 23 Oct – 11 Nov", ProjectText.Meta([Task(-9, -7, done: true), Task(-1, 10)]));

    [Fact]
    public void Meta_without_tasks() => Assert.Equal("No tasks yet", ProjectText.Meta([]));

    [Fact]
    public void Drag_label_for_one_day_and_many()
    {
        Assert.Equal("Thu 5 Nov", ProjectText.DragLabel(Today.AddDays(4), Today.AddDays(4)));
        Assert.Equal("Thu 5 Nov → Mon 9 Nov · 5 days", ProjectText.DragLabel(Today.AddDays(4), Today.AddDays(8)));
    }

    [Fact]
    public void Accessible_name_says_overdue_or_done()
    {
        Assert.Equal("Timeline page, Sat 31 Oct to Thu 5 Nov", ProjectText.Accessible(Task(-1, 4), Today));
        Assert.Equal("Timeline page, Wed 28 Oct to Sat 31 Oct, overdue", ProjectText.Accessible(Task(-4, -1), Today));
        Assert.Equal("Timeline page, Wed 28 Oct to Sat 31 Oct, done", ProjectText.Accessible(Task(-4, -1, done: true), Today));
    }

    [Fact]
    public void Month_adds_the_year_only_when_it_is_another()
    {
        Assert.Equal("November", ProjectText.Month(new DateOnly(2026, 11, 1), 2026));
        Assert.Equal("January 2027", ProjectText.Month(new DateOnly(2027, 1, 1), 2026));
        Assert.Equal("November 2026", ProjectText.MonthYear(new DateOnly(2026, 11, 20)));
    }

    [Fact]
    public void Overdue_is_an_open_task_that_ended_before_today()
    {
        Assert.True(Task(-4, -1).IsOverdue(Today));
        Assert.False(Task(-4, 0).IsOverdue(Today));
        Assert.False(Task(-4, -1, done: true).IsOverdue(Today));
        Assert.Equal(4, Task(-4, -1).Days);
    }
}
