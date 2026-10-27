namespace Gili.Core.Todos;

/// <summary>
/// A task (F-TOOL-06): its title, the day it is due and an optional local time, the pending reminder in UTC (null once
/// it showed, when there is no time, or when the time had already passed), when it was done, and its place in the list.
/// </summary>
public sealed record Todo(
    long Id,
    string Title,
    DateOnly? DueDate,
    TimeOnly? DueTime,
    DateTimeOffset? RemindAt,
    DateTimeOffset? DoneAt,
    int SortOrder,
    DateTimeOffset CreatedAt)
{
    public bool IsDone => DoneAt is not null;
}
