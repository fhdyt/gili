using Gili.Core.Modules;

namespace Gili.Core.Tests.Modules;

public class NetworkTodayTests
{
    private static readonly DateTime Morning = new(2026, 10, 27, 9, 0, 0);

    [Fact]
    public void Today_adds_up_each_second()
    {
        var today = new NetworkToday();
        today.Add(100, 1000, Morning);
        today.Add(50, 500, Morning.AddSeconds(1));
        Assert.Equal((150.0, 1500.0), today.Total);
    }

    [Fact]
    public void A_new_day_starts_from_zero()
    {
        var today = new NetworkToday();
        today.Add(100, 1000, Morning);
        today.Add(7, 8, Morning.Date.AddDays(1).AddSeconds(1));
        Assert.Equal((7.0, 8.0), today.Total);
    }

    [Fact]
    public void Tooltip_reads_adapters_then_today()
    {
        var today = new NetworkToday();
        today.Add(1_258_291, 47_185_920, Morning);
        Assert.Equal("Wi‑Fi\nToday · ↑ 1.2 MB  ↓ 45.0 MB", today.Tooltip("Wi‑Fi"));
    }

    [Theory]
    [InlineData(512, "512 B")]
    [InlineData(2048, "2 KB")]
    [InlineData(5_242_880, "5.0 MB")]
    [InlineData(3_221_225_472, "3.0 GB")]
    public void Amounts_use_the_module_units(double bytes, string text) => Assert.Equal(text, NetworkToday.Amount(bytes));
}
