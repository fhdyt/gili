using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Gili.Core.Formatting;
using Gili.Core.Layout;
using Gili.Core.Settings;

namespace Gili.App.Bar;

public sealed partial class BarViewModel : ObservableObject
{
    [ObservableProperty] private Brush _tint = Brushes.Transparent;
    [ObservableProperty] private double _moduleHeight = 26;
    [ObservableProperty] private string _cpuText = "—";
    [ObservableProperty] private string? _cpuTooltip;
    [ObservableProperty] private string _ramText = "—";
    [ObservableProperty] private string? _ramTooltip;
    [ObservableProperty] private string _upText = "↑ —";
    [ObservableProperty] private string _downText = "↓ —";
    [ObservableProperty] private string? _netTooltip;
    [ObservableProperty] private string _clockTime = "";
    [ObservableProperty] private string _clockDate = "";
    [ObservableProperty] private string _clockTooltip = "";

    /// <summary>The widest the time can be, laid under it so the bar never shifts as the digits tick (PRD 6.4).</summary>
    [ObservableProperty] private string _clockTemplate = "00:00:00";

    public void ApplyClock(DateTime now, AppSettings settings)
    {
        ClockTime = ClockText.BarTime(now, settings.Use24HourClock, settings.ShowSeconds);
        ClockDate = ClockText.BarDate(now);
        ClockTooltip = ClockText.LongDate(now);
        ClockTemplate = ClockText.BarTime(new DateTime(2000, 1, 1, 20, 0, 0), settings.Use24HourClock, settings.ShowSeconds)
            .Replace('2', '0');
    }

    /// <summary>Each second only the time text changes; the bar is laid out again only when the minute changes.</summary>
    public void ApplySecond(DateTime now, AppSettings settings) =>
        ClockTime = ClockText.BarTime(now, settings.Use24HourClock, settings.ShowSeconds);

    public void ApplySettings(AppSettings settings, BarAppearance look)
    {
        var brush = new SolidColorBrush(Color.FromArgb((byte)Math.Round(look.TintOpacity * 255), 0x16, 0x16, 0x18));
        brush.Freeze();
        Tint = brush;
        ModuleHeight = BarMetrics.ModuleHitHeight(settings.BarHeight);
    }

    public void ApplyStats(StatsSnapshot stats)
    {
        var cpu = stats.Cpu.Value;
        CpuText = cpu is null ? "—" : StatText.Percent(cpu.Percent);
        CpuTooltip = stats.Cpu.Error;

        var memory = stats.Memory.Value;
        RamText = memory is null ? "—" : StatText.Percent(memory.Percent);
        RamTooltip = memory is null ? stats.Memory.Error : StatText.MemoryUsage(memory.UsedBytes, memory.TotalBytes);

        var network = stats.Network.Value;
        UpText = "↑ " + (network is null ? "—" : StatText.Rate(network.UpBytesPerSecond));
        DownText = "↓ " + (network is null ? "—" : StatText.Rate(network.DownBytesPerSecond));
        NetTooltip = network is null ? stats.Network.Error : network.Adapters;
    }
}
