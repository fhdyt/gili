using Gili.Core.Modules;
using Gili.Core.Settings;

namespace Gili.Core.Tests.Modules;

public class StatsRulesTests
{
    private const RightModules AllSystem =
        RightModules.Weather | RightModules.Network | RightModules.Cpu | RightModules.Ram | RightModules.Gpu | RightModules.Disk;

    [Fact]
    public void Nothing_shown_samples_nothing() => Assert.Equal(StatsDemand.None, StatsDemands.For(AllSystem, systemPageOpen: false));

    [Fact]
    public void System_page_asks_for_everything() => Assert.Equal(StatsDemand.All, StatsDemands.For(AllSystem, systemPageOpen: true));

    [Fact]
    public void Modules_ask_for_their_own()
    {
        Assert.Equal(StatsDemand.Cpu | StatsDemand.Memory | StatsDemand.Network, StatsDemands.For(new AppSettings().HiddenModules, false));
        Assert.Equal(StatsDemand.Gpu | StatsDemand.Disk, StatsDemands.For(AllSystem & ~(RightModules.Gpu | RightModules.Disk), false));
        Assert.Equal(StatsDemand.None, StatsDemands.For(AllSystem & ~RightModules.Weather, false));
    }

    [Fact]
    public void Engines_sum_per_engine_and_take_the_max() =>
        Assert.Equal(5, GpuEngines.Utilization(
        [
            ("pid_1_luid_0x00000000_0x00011a0e_phys_0_eng_0_engtype_3D", 2),
            ("pid_2_luid_0x00000000_0x00011a0e_phys_0_eng_0_engtype_3D", 3),
            ("pid_1_luid_0x00000000_0x00011a0e_phys_0_eng_1_engtype_Copy", 4),
        ]));

    [Fact]
    public void Engine_sum_is_capped_at_100() =>
        Assert.Equal(100, GpuEngines.Utilization(
        [
            ("pid_1_luid_0x0_0x1_phys_0_eng_0_engtype_3D", 70),
            ("pid_2_luid_0x0_0x1_phys_0_eng_0_engtype_3D", 60),
        ]));

    [Fact]
    public void No_engines_reads_zero() => Assert.Equal(0, GpuEngines.Utilization([]));

    [Fact]
    public void History_keeps_the_last_60()
    {
        var history = new MetricHistory();
        for (var i = 0; i < 65; i++) history.Add(i);
        Assert.Equal(60, history.Values.Count);
        Assert.Equal(5, history.Values[0]);
        Assert.Equal(64, history.Values[^1]);
        history.Clear();
        Assert.Empty(history.Values);
    }

    [Fact]
    public void Sparkline_ends_at_the_right_edge()
    {
        var points = Sparkline.Points([0, 50, 100], max: 100, width: 100, height: 26);
        Assert.Equal(3, points.Count);
        Assert.Equal(100, points[2].X, 6);
        Assert.Equal(100 - 100.0 / 59, points[1].X, 6);
        Assert.Equal(100 - 200.0 / 59, points[0].X, 6);
        Assert.Equal(25, points[0].Y, 6);
        Assert.Equal(13.5, points[1].Y, 6);
        Assert.Equal(2, points[2].Y, 6);
    }

    [Fact]
    public void Sparkline_clamps_values_over_the_scale() =>
        Assert.Equal(2, Sparkline.Points([500], max: 100, width: 100, height: 26)[0].Y, 6);

    [Fact]
    public void Download_scale_has_a_floor()
    {
        Assert.Equal(102400, Sparkline.DownloadMax([0, 0, 0]));
        Assert.Equal(102400, Sparkline.DownloadMax([]));
        Assert.Equal(2_000_000, Sparkline.DownloadMax([10, 2_000_000, 5]));
    }
}
