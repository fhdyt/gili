using System.Globalization;
using System.Text.RegularExpressions;
using Gili.Core.Alarms;

namespace Gili.Core.Todos;

/// <summary>The texts on the Calendar and Todo pages and the reminder card (mockup renderCev, renderTodos, CARDS.todo).</summary>
public static class TodoText
{
    public const int TitleLimit = 60;
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
    public const string CalendarNote = "New tasks are added from Calendar in the island.";

    private static readonly string[] Months = ["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec"];
    private static readonly string[] Weekdays = ["sun", "mon", "tue", "wed", "thu", "fri", "sat"];
    private static readonly Regex IsoDate = new(@"^([0-9]{4})-([0-9]{1,2})-([0-9]{1,2})$", RegexOptions.Compiled);
    private static readonly Regex NamedDate = new(@"^(?:([a-z]{3}) )?([0-9]{1,2}) ([a-z]{3})(?: ([0-9]{4}))?$", RegexOptions.Compiled);

    /// <summary>The title as saved: trimmed and at most 60 characters; null when nothing is left.</summary>
    public static string? Title(string text)
    {
        var title = text.Trim();
        if (title.Length == 0) return null;
        return title.Length > TitleLimit ? title[..TitleLimit] : title;
    }

    public static string Day(DateOnly date) => date.ToString("ddd d MMM", English);

    public static string Heading(DateOnly date, DateOnly today) => date == today ? "Today" : Day(date);

    public static string Month(int year, int month) => new DateOnly(year, month, 1).ToString("MMMM yyyy", English);

    public static string CalendarItem(Todo todo, bool use24Hour) =>
        $"{(todo.DueTime is { } time ? AlarmText.Time(time, use24Hour) : "Task")} · {todo.Title}";

    public static string AlarmItem(Alarm alarm, bool use24Hour) => $"{AlarmText.Time(alarm.Time, use24Hour)} · Alarm: {AlarmText.Label(alarm)}";

    public static string Added(DateOnly date, TimeOnly? time, bool use24Hour) =>
        $"Added to {Day(date)}{(time is { } t ? " · " + AlarmText.Time(t, use24Hour) : "")}";

    public static string Count(IReadOnlyList<Todo> today) => $"{today.Count(t => t.IsDone)}/{today.Count}";

    public static string CardSubtitle(Todo todo, DateOnly today, bool use24Hour)
    {
        var day = todo.DueDate is not { } due || due == today ? "today" : Day(due);
        return todo.DueTime is { } time ? $"Due {AlarmText.Time(time, use24Hour)} {day}" : $"Due {day}";
    }

    /// <summary>
    /// The Date field (mockup dateParse): yyyy-MM-dd, d MMM, d MMM yyyy, ddd d MMM, today or tomorrow in any case; no year
    /// means this year. Blank is true with no date; anything else is false.
    /// </summary>
    public static bool TryDate(string text, DateOnly today, out DateOnly? date)
    {
        date = null;
        var t = text.Trim().ToLowerInvariant();
        if (t.Length == 0) return true;
        if (t == "today") { date = today; return true; }
        if (t == "tomorrow") { date = today.AddDays(1); return true; }
        int year, month, day;
        if (IsoDate.Match(t) is { Success: true } iso)
        {
            year = int.Parse(iso.Groups[1].Value, CultureInfo.InvariantCulture);
            month = int.Parse(iso.Groups[2].Value, CultureInfo.InvariantCulture);
            day = int.Parse(iso.Groups[3].Value, CultureInfo.InvariantCulture);
        }
        else if (NamedDate.Match(t) is { Success: true } named)
        {
            if (named.Groups[1].Success && Array.IndexOf(Weekdays, named.Groups[1].Value) < 0) return false;
            month = Array.IndexOf(Months, named.Groups[3].Value) + 1;
            day = int.Parse(named.Groups[2].Value, CultureInfo.InvariantCulture);
            year = named.Groups[4].Success ? int.Parse(named.Groups[4].Value, CultureInfo.InvariantCulture) : today.Year;
        }
        else return false;
        if (year < 1 || year > 9999 || month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month)) return false;
        date = new DateOnly(year, month, day);
        return true;
    }

    /// <summary>The Date field filled from a task: the shown form (Thu 30 Oct, with the year when it is not this year), or blank without a date.</summary>
    public static string DateInput(DateOnly? date, DateOnly today) => date is { } d ? DayWithYear(d, today) : "";

    /// <summary>The Date field filled from a task (mockup dispDate): Today or Tomorrow for those days, else <see cref="DateInput"/>; blank without a date.</summary>
    public static string DateField(DateOnly? date, DateOnly today) =>
        date is not { } d ? "" : d == today ? "Today" : d == today.AddDays(1) ? "Tomorrow" : DayWithYear(d, today);

    /// <summary>The date on a Todo tab row: Today, Tomorrow or the day, or No date, plus " · 15:00" when it has a time.</summary>
    public static string Due(Todo todo, DateOnly today, bool use24Hour)
    {
        var day = todo.DueDate is not { } due ? "No date" : due == today ? "Today" : due == today.AddDays(1) ? "Tomorrow" : DayWithYear(due, today);
        return todo.DueTime is { } time ? $"{day} · {AlarmText.Time(time, use24Hour)}" : day;
    }

    private static string DayWithYear(DateOnly date, DateOnly today) => date.Year == today.Year ? Day(date) : $"{Day(date)} {date.Year}";

    /// <summary>An open task whose day has passed shows Overdue instead of its date.</summary>
    public static bool IsOverdue(Todo todo, DateOnly today) => !todo.IsDone && todo.DueDate is { } due && due < today;

    /// <summary>The text under an empty Todo list.</summary>
    public static string Empty(TodoFilter filter) => filter switch
    {
        TodoFilter.Today => "No tasks for today",
        TodoFilter.Done => "No finished tasks",
        _ => "No tasks yet",
    };
}
