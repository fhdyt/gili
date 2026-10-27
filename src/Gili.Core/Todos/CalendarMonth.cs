namespace Gili.Core.Todos;

/// <summary>The month grid on the Calendar page, Monday first (mockup buildCal).</summary>
public static class CalendarMonth
{
    /// <summary>Empty cells before the 1st, and the number of days.</summary>
    public static (int Leading, int Days) Cells(int year, int month) =>
        (((int)new DateOnly(year, month, 1).DayOfWeek + 6) % 7, DateTime.DaysInMonth(year, month));

    public static int Rows(int year, int month)
    {
        var (leading, days) = Cells(year, month);
        return (leading + days + 6) / 7;
    }

    public static (int Year, int Month) Step(int year, int month, int by)
    {
        var first = new DateOnly(year, month, 1).AddMonths(by);
        return (first.Year, first.Month);
    }
}
