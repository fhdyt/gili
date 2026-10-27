using Gili.Core.Todos;

namespace Gili.Core.Tests.Todos;

public class CalendarMonthTests
{
    [Fact]
    public void October_2026_starts_on_thursday() => Assert.Equal((3, 31), CalendarMonth.Cells(2026, 10));

    [Fact]
    public void A_month_starting_on_sunday_needs_six_rows()
    {
        // March 2026 starts on a Sunday and has 31 days.
        Assert.Equal((6, 31), CalendarMonth.Cells(2026, 3));
        Assert.Equal(6, CalendarMonth.Rows(2026, 3));
        Assert.Equal(5, CalendarMonth.Rows(2026, 10));
    }

    [Fact]
    public void Stepping_months_crosses_years()
    {
        Assert.Equal((2027, 1), CalendarMonth.Step(2026, 12, 1));
        Assert.Equal((2025, 12), CalendarMonth.Step(2026, 1, -1));
    }
}
