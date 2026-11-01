using System.Globalization;
using Gili.Core.Todos;

namespace Gili.Core.Projects;

/// <summary>The texts of the Projects page (mockup renderPj, pjBar, pjDragMove).</summary>
public static class ProjectText
{
    public const int NameLimit = 60;
    public const int TaskLimit = 80;
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    /// <summary>A name as saved: trimmed and cut to <paramref name="limit"/>; null when nothing is left.</summary>
    public static string? Name(string text, int limit)
    {
        var name = text.Trim();
        if (name.Length == 0) return null;
        return name.Length > limit ? name[..limit] : name;
    }

    /// <summary>"3/8 done · 23 Oct – 11 Nov", or "No tasks yet".</summary>
    public static string Meta(IReadOnlyCollection<ProjectTask> tasks) => tasks.Count == 0
        ? "No tasks yet"
        : $"{Progress(tasks)} done · {Short(tasks.Min(t => t.Start))} – {Short(tasks.Max(t => t.End))}";

    /// <summary>"3/8": done tasks over all of them, as on the project list.</summary>
    public static string Progress(IReadOnlyCollection<ProjectTask> tasks) => $"{tasks.Count(t => t.IsDone)}/{tasks.Count}";

    public static string Short(DateOnly date) => date.ToString("d MMM", English);

    /// <summary>The label over a bar being dragged: "Thu 5 Nov", or "Thu 5 Nov → Mon 9 Nov · 5 days".</summary>
    public static string DragLabel(DateOnly start, DateOnly end) => start == end
        ? TodoText.Day(start)
        : $"{TodoText.Day(start)} → {TodoText.Day(end)} · {end.DayNumber - start.DayNumber + 1} days";

    /// <summary>A bar's accessible name: "Timeline page, Sat 31 Oct to Thu 5 Nov, overdue".</summary>
    public static string Accessible(ProjectTask task, DateOnly today) =>
        $"{task.Title}, {TodoText.Day(task.Start)} to {TodoText.Day(task.End)}" + (task.IsDone ? ", done" : task.IsOverdue(today) ? ", overdue" : "");

    /// <summary>The label at the first of a month: "November", with the year only when it is not this year.</summary>
    public static string Month(DateOnly date, int thisYear) =>
        date.ToString(date.Year == thisYear ? "MMMM" : "MMMM yyyy", English);

    /// <summary>The month that stays at the left of the timeline: "November 2026".</summary>
    public static string MonthYear(DateOnly date) => date.ToString("MMMM yyyy", English);

    /// <summary>A date field's text: "Sun 1 Nov", with the year when it is not this year.</summary>
    public static string Field(DateOnly date, DateOnly today) => TodoText.DateInput(date, today);

    /// <summary>A date field read back; blank or anything that is not a date is null.</summary>
    public static DateOnly? ParseField(string text, DateOnly today) =>
        TodoText.TryDate(text.Replace(",", "", StringComparison.Ordinal), today, out var date) ? date : null;
}
