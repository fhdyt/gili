namespace Gili.Core.Projects;

/// <summary>The days a timeline can scroll over, first and last included.</summary>
public readonly record struct TimelineSpan(DateOnly First, DateOnly Last)
{
    public int Days => Last.DayNumber - First.DayNumber + 1;
}

/// <summary>Where the Projects timeline can scroll and where it starts (PRD 6.3 "Halaman Projects").</summary>
public static class TimelineRange
{
    /// <summary>Days kept before the earliest day (the first task or today) and after the latest.</summary>
    public const int Before = 35, After = 60;

    /// <summary>A project too wide for the view by less than this still counts as not fitting, so its ends never touch the edges.</summary>
    private const double FitMargin = 60;

    public static TimelineSpan Of(IReadOnlyCollection<ProjectTask> tasks, DateOnly today) => new(
        Min(tasks.Count == 0 ? today : tasks.Min(t => t.Start), today).AddDays(-Before),
        Max(tasks.Count == 0 ? today : tasks.Max(t => t.End), today).AddDays(After));

    /// <summary>The left edge of a day's column.</summary>
    public static double X(TimelineSpan span, DateOnly day, double dayWidth) => (day.DayNumber - span.First.DayNumber) * dayWidth;

    /// <summary>The scroll a project opens at: all of it centred when it fits, otherwise today a third of the way in.</summary>
    public static double Home(IReadOnlyCollection<ProjectTask> tasks, DateOnly today, TimelineSpan span, double dayWidth, double viewWidth)
    {
        if (tasks.Count > 0)
        {
            var first = tasks.Min(t => t.Start);
            var days = tasks.Max(t => t.End).DayNumber - first.DayNumber + 1;
            if (days * dayWidth <= viewWidth - FitMargin)
                return Clamp(X(span, first, dayWidth) + days * dayWidth / 2 - viewWidth / 2, span, dayWidth, viewWidth);
        }
        return Today(today, span, dayWidth, viewWidth);
    }

    /// <summary>The scroll that puts today a third of the way in (the Today button).</summary>
    public static double Today(DateOnly today, TimelineSpan span, double dayWidth, double viewWidth) =>
        Clamp(X(span, today, dayWidth) - viewWidth / 3, span, dayWidth, viewWidth);

    public static double Clamp(double offset, TimelineSpan span, double dayWidth, double viewWidth) =>
        Math.Clamp(offset, 0, Math.Max(0, span.Days * dayWidth - viewWidth));

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
}
