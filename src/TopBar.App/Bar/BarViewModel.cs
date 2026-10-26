using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using TopBar.Core.Formatting;
using TopBar.Core.Layout;
using TopBar.Core.Settings;

namespace TopBar.App.Bar;

public sealed partial class BarViewModel : ObservableObject
{
    private readonly DispatcherTimer _clockTimer = new(DispatcherPriority.Normal);
    private bool _use24Hour = true;

    [ObservableProperty] private Brush _tint = Brushes.Transparent;
    [ObservableProperty] private double _islandHeight = 24;
    [ObservableProperty] private CornerRadius _islandCornerRadius = new(12);
    [ObservableProperty] private double _islandFontSize = 12;
    [ObservableProperty] private double _moduleHeight = 26;
    [ObservableProperty] private string _time = "";
    [ObservableProperty] private string _date = "";
    [ObservableProperty] private string _cpuText = "—";
    [ObservableProperty] private string? _cpuTooltip;
    [ObservableProperty] private string _ramText = "—";
    [ObservableProperty] private string? _ramTooltip;
    [ObservableProperty] private string _upText = "↑ —";
    [ObservableProperty] private string _downText = "↓ —";
    [ObservableProperty] private string? _netTooltip;

    public BarViewModel() => _clockTimer.Tick += (_, _) => RefreshClock();

    public void ApplySettings(AppSettings settings, BarAppearance look)
    {
        _use24Hour = settings.Use24HourClock;
        var brush = new SolidColorBrush(Color.FromArgb((byte)Math.Round(look.TintOpacity * 255), 0x16, 0x16, 0x18));
        brush.Freeze();
        Tint = brush;
        IslandHeight = settings.IslandHeight;
        IslandCornerRadius = new CornerRadius(BarMetrics.IslandCornerRadius(settings.IslandHeight));
        IslandFontSize = BarMetrics.IslandFontSize(settings.IslandHeight);
        ModuleHeight = BarMetrics.ModuleHitHeight(settings.BarHeight);
        RefreshClock();
    }

    /// <summary>Shows the current minute and re-arms the timer just past the next minute boundary (F-ISL-01).</summary>
    public void RefreshClock()
    {
        var now = DateTime.Now;
        Time = ClockText.Time(now, _use24Hour);
        Date = ClockText.Date(now);
        _clockTimer.Stop();
        _clockTimer.Interval = ClockText.UntilNextMinute(now) + TimeSpan.FromMilliseconds(50);
        _clockTimer.Start();
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
