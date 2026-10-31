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
    public void The_date_field_says_Today_and_Tomorrow_and_reads_them_back()
    {
        Assert.Equal("", TodoText.DateField(null, Tuesday));
        Assert.Equal("Today", TodoText.DateField(Tuesday, Tuesday));
        Assert.Equal("Tomorrow", TodoText.DateField(Tuesday.AddDays(1), Tuesday));
        Assert.Equal("Fri 30 Oct", TodoText.DateField(new DateOnly(2026, 10, 30), Tuesday));
        Assert.Equal("Wed 3 Nov 2027", TodoText.DateField(new DateOnly(2027, 11, 3), Tuesday));
        foreach (var day in new DateOnly?[] { null, Tuesday, Tuesday.AddDays(1), new DateOnly(2027, 11, 3) })
        {
            Assert.True(TodoText.TryDate(TodoText.DateField(day, Tuesday), Tuesday, out var back));
            Assert.Equal(day, back);
        }
    }

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

    [Theory]
    [InlineData("2026-10-30", 2026, 10, 30)]
    [InlineData("30 Oct", 2026, 10, 30)]
    [InlineData("30 oct", 2026, 10, 30)]
    [InlineData("Thu 30 Oct", 2026, 10, 30)]
    [InlineData("30 Oct 2027", 2027, 10, 30)]
    [InlineData("today", 2026, 10, 27)]
    [InlineData("Tomorrow", 2026, 10, 28)]
    public void Date_input_reads_the_shown_forms(string text, int y, int m, int d)
    {
        Assert.True(TodoText.TryDate(text, Tuesday, out var date));
        Assert.Equal(new DateOnly(y, m, d), date);
    }

    [Theory]
    [InlineData("٣٠ Oct")]
    [InlineData("31 Feb")]
    [InlineData("abc")]
    [InlineData("30/10")]
    public void Date_input_refuses_nonsense_and_non_ascii_digits(string text)
    {
        Assert.False(TodoText.TryDate(text, Tuesday, out _));
    }

    [Fact]
    public void A_blank_date_input_means_no_date()
    {
        Assert.True(TodoText.TryDate("", Tuesday, out var date));
        Assert.Null(date);
        Assert.True(TodoText.TryDate("  ", Tuesday, out date));
        Assert.Null(date);
        Assert.Equal("", TodoText.DateInput(null, Tuesday));
        Assert.Equal("Fri 30 Oct", TodoText.DateInput(new DateOnly(2026, 10, 30), Tuesday));
    }

    [Fact]
    public void A_date_without_a_year_stays_in_this_year()
    {
        Assert.True(TodoText.TryDate("20 Oct", new DateOnly(2026, 10, 29), out var date));
        Assert.Equal(new DateOnly(2026, 10, 20), date);
    }

    [Fact]
    public void Due_reads_today_tomorrow_or_the_day_with_its_time()
    {
        Assert.Equal("Today · 15:00", TodoText.Due(Task("a", new TimeOnly(15, 0)), Tuesday, true));
        Assert.Equal("Today · 3:00 PM", TodoText.Due(Task("a", new TimeOnly(15, 0)), Tuesday, false));
        Assert.Equal("Today", TodoText.Due(Task("a"), Tuesday, true));
        Assert.Equal("Tomorrow", TodoText.Due(Task("a", due: Tuesday.AddDays(1)), Tuesday, true));
        Assert.Equal("Tue 3 Nov", TodoText.Due(Task("a", due: Tuesday.AddDays(7)), Tuesday, true));
        Assert.Equal("No date", TodoText.Due(Task("a") with { DueDate = null }, Tuesday, true));
    }

    [Fact]
    public void Overdue_only_for_open_tasks_before_today()
    {
        Assert.True(TodoText.IsOverdue(Task("a", due: Tuesday.AddDays(-1)), Tuesday));
        Assert.False(TodoText.IsOverdue(Task("a", due: Tuesday), Tuesday));
        Assert.False(TodoText.IsOverdue(Task("a", due: Tuesday.AddDays(-1), done: true), Tuesday));
        Assert.False(TodoText.IsOverdue(Task("a") with { DueDate = null }, Tuesday));
    }

    [Fact]
    public void Empty_texts_per_filter()
    {
        Assert.Equal("No tasks for today", TodoText.Empty(TodoFilter.Today));
        Assert.Equal("No tasks yet", TodoText.Empty(TodoFilter.All));
        Assert.Equal("No finished tasks", TodoText.Empty(TodoFilter.Done));
        Assert.Equal("New tasks are added from Calendar in the island.", TodoText.CalendarNote);
    }

    [Theory]
    [InlineData(2027, 11, 3)]
    [InlineData(2028, 2, 29)]
    [InlineData(2026, 10, 30)]
    public void Another_years_date_round_trips_through_the_editor(int y, int m, int d)
    {
        var original = new DateOnly(y, m, d);
        Assert.True(TodoText.TryDate(TodoText.DateInput(original, Tuesday), Tuesday, out var date));
        Assert.Equal(original, date);
    }

    [Fact]
    public void Another_years_date_shows_its_year()
    {
        Assert.Equal("Wed 3 Nov 2027", TodoText.DateInput(new DateOnly(2027, 11, 3), Tuesday));
        Assert.Equal("Wed 3 Nov 2027 · 15:00", TodoText.Due(Task("a", new TimeOnly(15, 0), new DateOnly(2027, 11, 3)), Tuesday, true));
    }
}
