namespace Gili.Core.Projects;

/// <summary>A project on the Projects page (F-ISL-34): its name and place in the project list.</summary>
public sealed record Project(long Id, string Name, int SortOrder, DateTimeOffset CreatedAt);

/// <summary>
/// One task on a project's timeline: the local days it runs from and to (both included), its note colour (0-7), when it
/// was done, and its row. Days are local dates, so changing time zone never moves a task.
/// </summary>
public sealed record ProjectTask(
    long Id,
    long ProjectId,
    string Title,
    DateOnly Start,
    DateOnly End,
    int Color,
    DateTimeOffset? DoneAt,
    int SortOrder,
    DateTimeOffset CreatedAt)
{
    public bool IsDone => DoneAt is not null;

    public int Days => End.DayNumber - Start.DayNumber + 1;

    /// <summary>Still open after its last day: the bar gets a red ring (PRD 6.3).</summary>
    public bool IsOverdue(DateOnly today) => !IsDone && End < today;
}
