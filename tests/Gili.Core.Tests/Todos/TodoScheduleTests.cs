using Gili.Core.Alarms;
using Gili.Core.Time;
using Gili.Core.Todos;

namespace Gili.Core.Tests.Todos;

public class TodoScheduleTests
{
    private static readonly TimeZoneInfo Jakarta = TimeZoneInfo.CreateCustomTimeZone("plus7", TimeSpan.FromHours(7), "+07", "+07");
    private static readonly DateTimeOffset Noon = new(2026, 10, 27, 12, 0, 0, TimeSpan.FromHours(7)); // Tuesday
    private static readonly DateOnly Tuesday = new(2026, 10, 27);

    private static Todo Task(long id, DateOnly? due, TimeOnly? time = null, DateTimeOffset? remind = null, bool done = false, int order = 0) =>
        new(id, $"t{id}", due, time, remind, done ? Noon : null, order, Noon);

    [Fact]
    public void A_future_time_gets_a_reminder() =>
        Assert.Equal(new DateTimeOffset(2026, 10, 27, 15, 0, 0, TimeSpan.FromHours(7)),
            TodoSchedule.Arm(Task(1, Tuesday, new TimeOnly(15, 0)), Noon, Jakarta).RemindAt);

    [Fact]
    public void A_past_time_today_has_no_reminder()
    {
        Assert.Null(TodoSchedule.Arm(Task(1, Tuesday, new TimeOnly(9, 0)), Noon, Jakarta).RemindAt);
        Assert.True(TodoSchedule.IsPastToday(Tuesday, new TimeOnly(9, 0), Noon, Jakarta));
        Assert.False(TodoSchedule.IsPastToday(Tuesday, new TimeOnly(15, 0), Noon, Jakarta));
        Assert.False(TodoSchedule.IsPastToday(Tuesday.AddDays(1), new TimeOnly(9, 0), Noon, Jakarta));
    }

    [Fact]
    public void No_time_means_no_reminder() => Assert.Null(TodoSchedule.Arm(Task(1, Tuesday), Noon, Jakarta).RemindAt);

    [Fact]
    public void Done_todos_never_remind()
    {
        Assert.Null(TodoSchedule.Arm(Task(1, Tuesday, new TimeOnly(15, 0), done: true), Noon, Jakarta).RemindAt);
        Assert.Empty(TodoSchedule.Due([Task(1, Tuesday, remind: Noon.AddMinutes(-1), done: true)], Noon));
    }

    [Fact]
    public void Reminders_late_by_more_than_an_hour_are_dropped()
    {
        Todo[] todos = [Task(1, Tuesday, remind: Noon.AddMinutes(-59)), Task(2, Tuesday, remind: Noon.AddMinutes(-61)), Task(3, Tuesday, remind: Noon.AddMinutes(5))];
        Assert.Equal([1L], TodoSchedule.Due(todos, Noon).Select(t => t.Id));
        Assert.Equal([2L], TodoSchedule.Stale(todos, Noon).Select(t => t.Id));
        // The service clears due and stale reminders first; what is left waits for the soonest.
        Assert.Equal(3, TodoSchedule.Next([todos[2], Task(4, Tuesday, remind: Noon.AddMinutes(9))])!.Id);
    }

    [Fact]
    public void Snooze_is_ten_minutes_from_now() =>
        Assert.Equal(Noon.AddMinutes(10), TodoSchedule.Snooze(Task(1, Tuesday, new TimeOnly(12, 0)), Noon).RemindAt);

    [Fact]
    public void Today_is_undated_or_due_today_in_order()
    {
        Todo[] todos = [Task(1, Tuesday, order: 2), Task(2, null, order: 1), Task(3, Tuesday.AddDays(1), order: 0), Task(4, Tuesday.AddDays(-1), order: 3)];
        Assert.Equal([2L, 1L], TodoSchedule.Today(todos, Tuesday).Select(t => t.Id));
        Assert.Equal([3L], TodoSchedule.On(todos, Tuesday.AddDays(1)).Select(t => t.Id));
    }

    [Fact]
    public void Today_follows_the_clock()
    {
        Assert.Equal(Tuesday, TodoSchedule.LocalDate(Noon, Jakarta));
        Assert.Equal(Tuesday.AddDays(1), TodoSchedule.LocalDate(new DateTimeOffset(2026, 10, 27, 17, 30, 0, TimeSpan.Zero), Jakarta));
    }

    [Fact]
    public void Repeating_alarms_list_on_matching_days_but_give_no_dot()
    {
        var weekdays = new Alarm(1, new TimeOnly(5, 30), Days.Weekdays, "Wake up", "Radar", true, null);
        var once = new Alarm(2, new TimeOnly(21, 0), Days.None, "Pack", "Soft", true, new DateTimeOffset(2026, 10, 29, 21, 0, 0, TimeSpan.FromHours(7)));
        var off = new Alarm(3, new TimeOnly(6, 0), Days.Every, "Off", "Radar", false, null);
        Alarm[] alarms = [weekdays, once, off];
        var thursday = new DateOnly(2026, 10, 29);
        var saturday = new DateOnly(2026, 10, 31);
        Assert.Equal([1L, 2L], TodoSchedule.AlarmsOn(alarms, thursday, Jakarta).Select(a => a.Id));
        Assert.Empty(TodoSchedule.AlarmsOn(alarms, saturday, Jakarta));
        Assert.True(TodoSchedule.HasDot([], alarms, thursday, Jakarta));
        Assert.False(TodoSchedule.HasDot([], alarms, Tuesday, Jakarta));
        Assert.True(TodoSchedule.HasDot([Task(9, Tuesday)], alarms, Tuesday, Jakarta));
    }

    [Fact]
    public void Filters_split_today_all_and_done()
    {
        Todo[] todos =
        [
            Task(4, Tuesday.AddDays(3), order: 0),
            Task(3, null, order: 1),
            Task(2, Tuesday, order: 1),
            Task(1, Tuesday.AddDays(-2), done: true, order: 3),
            Task(5, Tuesday, done: true, order: 4),
        ];
        Assert.Equal([2L, 3L, 5L], TodoSchedule.Filter(todos, TodoFilter.Today, Tuesday).Select(t => t.Id));
        Assert.Equal([4L, 2L, 3L, 1L, 5L], TodoSchedule.Filter(todos, TodoFilter.All, Tuesday).Select(t => t.Id));
        Assert.Equal([1L, 5L], TodoSchedule.Filter(todos, TodoFilter.Done, Tuesday).Select(t => t.Id));
    }
}
