using Gili.Core.Time;

namespace Gili.Core.Tests.Time;

public class SchedulerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 5, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Missed_while_asleep_is_marked_missed()
    {
        var due = Scheduler.Due([new ScheduledItem("alarm", Now.AddHours(-2))], Now);
        Assert.Equal([new DueItem(new ScheduledItem("alarm", Now.AddHours(-2)), Missed: true)], due);
    }

    [Fact]
    public void Due_on_time_is_not_missed()
    {
        var due = Scheduler.Due([new ScheduledItem("timer", Now.AddSeconds(-60)), new ScheduledItem("later", Now.AddSeconds(1))], Now);
        Assert.Equal([new DueItem(new ScheduledItem("timer", Now.AddSeconds(-60)), Missed: false)], due);
    }

    [Fact]
    public void Due_items_come_in_time_order()
    {
        var due = Scheduler.Due([new ScheduledItem("b", Now.AddMinutes(-1)), new ScheduledItem("a", Now.AddMinutes(-5))], Now);
        Assert.Equal(["a", "b"], due.Select(d => d.Item.Id));
    }

    [Fact]
    public void Next_is_the_earliest_future_item()
    {
        ScheduledItem[] items = [new("past", Now.AddMinutes(-1)), new("soon", Now.AddMinutes(3)), new("later", Now.AddHours(1))];
        Assert.Equal(Now.AddMinutes(3), Scheduler.Next(items, Now));
        Assert.Null(Scheduler.Next([new ScheduledItem("past", Now.AddMinutes(-1))], Now));
    }
}
