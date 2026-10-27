using Gili.Core.Alarms;
using Gili.Core.Time;

namespace Gili.Core.Todos;

/// <summary>
/// Which tasks belong to a day, when their reminders show (F-TOOL-07), and which dates get a dot on the Calendar page.
/// </summary>
public static class TodoSchedule
{
    public static readonly TimeSpan SnoozeLength = TimeSpan.FromMinutes(10);

    /// <summary>A reminder found later than this on start or waking is dropped without a card (plan 2e decision 4).</summary>
    public static readonly TimeSpan LateLimit = TimeSpan.FromHours(1);

    public static DateOnly LocalDate(DateTimeOffset now, TimeZoneInfo zone) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);

    /// <summary>A task not done with a time still ahead gets its reminder; anything else has none.</summary>
    public static Todo Arm(Todo todo, DateTimeOffset now, TimeZoneInfo zone)
    {
        if (todo.IsDone || todo.DueTime is not { } time) return todo with { RemindAt = null };
        var at = AlarmRules.At(todo.DueDate ?? LocalDate(now, zone), time, zone);
        return todo with { RemindAt = at > now ? at : null };
    }

    /// <summary>A time today that has already gone: saved without a reminder, and its field gets a yellow edge.</summary>
    public static bool IsPastToday(DateOnly? date, TimeOnly? time, DateTimeOffset now, TimeZoneInfo zone) =>
        time is { } t && (date ?? LocalDate(now, zone)) == LocalDate(now, zone) && AlarmRules.At(LocalDate(now, zone), t, zone) <= now;

    /// <summary>Reminders that have come, at most an hour late, soonest first.</summary>
    public static IReadOnlyList<Todo> Due(IReadOnlyList<Todo> todos, DateTimeOffset now) =>
        [.. Pending(todos).Where(t => t.RemindAt <= now && now - t.RemindAt!.Value <= LateLimit).OrderBy(t => t.RemindAt)];

    /// <summary>Reminders more than an hour late, dropped without a card.</summary>
    public static IReadOnlyList<Todo> Stale(IReadOnlyList<Todo> todos, DateTimeOffset now) =>
        [.. Pending(todos).Where(t => now - t.RemindAt!.Value > LateLimit)];

    public static Todo? Next(IReadOnlyList<Todo> todos) => Pending(todos).OrderBy(t => t.RemindAt).FirstOrDefault();

    public static Todo Snooze(Todo todo, DateTimeOffset now) => todo with { RemindAt = now + SnoozeLength };

    /// <summary>The Todo page: tasks with no date and tasks due today, in the user's order (mockup isToday).</summary>
    public static IReadOnlyList<Todo> Today(IReadOnlyList<Todo> todos, DateOnly today) =>
        [.. todos.Where(t => t.DueDate is null || t.DueDate == today).OrderBy(t => t.SortOrder).ThenBy(t => t.Id)];

    public static IReadOnlyList<Todo> On(IReadOnlyList<Todo> todos, DateOnly date) =>
        [.. todos.Where(t => t.DueDate == date).OrderBy(t => t.DueTime ?? TimeOnly.MaxValue).ThenBy(t => t.SortOrder)];

    /// <summary>Alarms that are on and ring that day: a one-off on its date, a repeating one on its days.</summary>
    public static IReadOnlyList<Alarm> AlarmsOn(IReadOnlyList<Alarm> alarms, DateOnly date, TimeZoneInfo zone) =>
        [.. alarms.Where(a => a.Enabled && (a.Repeat == Days.None
                ? a.NextFireAt is { } at && LocalDate(at, zone) == date
                : a.Repeat.HasFlag(AlarmRules.DayOf(date.DayOfWeek))))
            .OrderBy(a => a.Time)];

    /// <summary>A dot for tasks or a one-off alarm; a daily alarm would put one on every date (plan 2e decision 2).</summary>
    public static bool HasDot(IReadOnlyList<Todo> todos, IReadOnlyList<Alarm> alarms, DateOnly date, TimeZoneInfo zone) =>
        todos.Any(t => t.DueDate == date) ||
        alarms.Any(a => a.Enabled && a.Repeat == Days.None && a.NextFireAt is { } at && LocalDate(at, zone) == date);

    private static IEnumerable<Todo> Pending(IReadOnlyList<Todo> todos) => todos.Where(t => !t.IsDone && t.RemindAt is not null);
}
