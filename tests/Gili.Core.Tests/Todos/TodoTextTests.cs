using Gili.Core.Alarms;
using Gili.Core.Time;
using Gili.Core.Todos;

namespace Gili.Core.Tests.Todos;

public class TodoTextTests
{
    private static readonly DateOnly Tuesday = new(2026, 10, 27);
    private static readonly DateTimeOffset Created = new(2026, 10, 27, 1, 0, 0, TimeSpan.Zero);

    private static Todo Task(string title, TimeOnly? time = null, DateOnly? due = null, bool done = false) =>
        new(1, title, due ?? Tuesday, time, null, done ? Created : null, 0, Created);

    [Fact]
    public void Blank_titles_are_refused()
    {
        Assert.Null(TodoText.Title("   "));
        Assert.Null(TodoText.Title(""));
        Assert.Equal("Send invoice", TodoText.Title("  Send invoice "));
        Assert.Equal(60, TodoText.Title(new string('a', 80))!.Length);
    }

    [Fact]
    public void Days_and_headings_read_like_the_mockup()
    {
        Assert.Equal("Wed 28 Oct", TodoText.Day(new DateOnly(2026, 10, 28)));
        Assert.Equal("Today", TodoText.Heading(Tuesday, Tuesday));
        Assert.Equal("Wed 28 Oct", TodoText.Heading(new DateOnly(2026, 10, 28), Tuesday));
        Assert.Equal("October 2026", TodoText.Month(2026, 10));
    }

    [Fact]
    public void Calendar_items_show_the_time_or_task()
    {
        Assert.Equal("15:00 · Prepare meeting slides", TodoText.CalendarItem(Task("Prepare meeting slides", new TimeOnly(15, 0)), use24Hour: true));
        Assert.Equal("Task · Send client invoice", TodoText.CalendarItem(Task("Send client invoice"), use24Hour: true));
        Assert.Equal("3:00 PM · Prepare meeting slides", TodoText.CalendarItem(Task("Prepare meeting slides", new TimeOnly(15, 0)), use24Hour: false));
        var alarm = new Alarm(1, new TimeOnly(14, 30), Days.Every, "Take medicine", "Radar", true, null);
        Assert.Equal("14:30 · Alarm: Take medicine", TodoText.AlarmItem(alarm, use24Hour: true));
    }

    [Fact]
    public void Added_confirmation_names_the_day_and_time()
    {
        Assert.Equal("Added to Wed 28 Oct", TodoText.Added(new DateOnly(2026, 10, 28), null, use24Hour: true));
        Assert.Equal("Added to Wed 28 Oct · 15:00", TodoText.Added(new DateOnly(2026, 10, 28), new TimeOnly(15, 0), use24Hour: true));
    }

    [Fact]
    public void Count_is_done_over_all() =>
        Assert.Equal("1/2", TodoText.Count([Task("a", done: true), Task("b")]));

    [Fact]
    public void Card_subtitle_says_when_it_is_due()
    {
        Assert.Equal("Due 15:00 today", TodoText.CardSubtitle(Task("x", new TimeOnly(15, 0)), Tuesday, use24Hour: true));
        Assert.Equal("Due 15:00 Wed 28 Oct", TodoText.CardSubtitle(Task("x", new TimeOnly(15, 0), new DateOnly(2026, 10, 28)), Tuesday, use24Hour: true));
    }
}
