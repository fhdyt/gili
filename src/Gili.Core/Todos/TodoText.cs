using System.Globalization;
using Gili.Core.Alarms;

namespace Gili.Core.Todos;

/// <summary>The texts on the Calendar and Todo pages and the reminder card (mockup renderCev, renderTodos, CARDS.todo).</summary>
public static class TodoText
{
    public const int TitleLimit = 60;
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

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
}
