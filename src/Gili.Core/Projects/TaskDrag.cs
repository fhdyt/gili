namespace Gili.Core.Projects;

/// <summary>What a drag on a timeline bar changes: the middle moves both days, either end only its own.</summary>
public enum DragEdge { Move, Start, End }

/// <summary>Dragging a bar on the timeline (PRD 6.3 "Halaman Projects"): pixels to whole days, and the days it lands on.</summary>
public static class TaskDrag
{
    /// <summary>A drag of <paramref name="pixels"/> in whole days; half a column or more counts as a day.</summary>
    public static int Days(double pixels, double dayWidth) => (int)Math.Round(pixels / dayWidth, MidpointRounding.AwayFromZero);

    /// <summary>The task's days after the drag; an end never passes the other, so a task is always at least a day.</summary>
    public static (DateOnly Start, DateOnly End) Apply(DragEdge edge, DateOnly start, DateOnly end, int days) => edge switch
    {
        DragEdge.Start => (Min(start.AddDays(days), end), end),
        DragEdge.End => (start, Max(end.AddDays(days), start)),
        _ => (start.AddDays(days), end.AddDays(days)),
    };

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
}
