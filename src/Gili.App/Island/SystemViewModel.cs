using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gili.App.Bar;
using Gili.Core.Formatting;
using Gili.Core.Modules;
using Gili.Platform.Shell;

namespace Gili.App.Island;

/// <summary>
/// The System page (PRD 6.3 page 10, mockup <c>renderSys</c>): CPU, RAM, GPU and download with their last minute, and
/// the free space on Windows' drive. It is fed only while the page shows; clicking it opens Task Manager.
/// </summary>
public sealed partial class SystemViewModel : ObservableObject
{
    private const double ChartHeight = 26;

    private readonly Action _close;
    private double _chartWidth = 100;
    private StatsSnapshot? _last;

    [ObservableProperty] private string _cpuText = "—";
    [ObservableProperty] private string _ramText = "—";
    [ObservableProperty] private string _gpuText = "—";
    [ObservableProperty] private string _downText = "—";
    [ObservableProperty] private string? _cpuTooltip;
    [ObservableProperty] private string? _ramTooltip;
    [ObservableProperty] private string? _gpuTooltip;
    [ObservableProperty] private string? _downTooltip;
    [ObservableProperty] private PointCollection _cpuPoints = [];
    [ObservableProperty] private PointCollection _ramPoints = [];
    [ObservableProperty] private PointCollection _gpuPoints = [];
    [ObservableProperty] private PointCollection _downPoints = [];
    [ObservableProperty] private string _diskTitle = "Disk C:";
    [ObservableProperty] private string _diskText = "—";
    [ObservableProperty] private string? _diskTooltip;
    [ObservableProperty] private GridLength _diskUsed = new(0, GridUnitType.Star);
    [ObservableProperty] private GridLength _diskFree = new(1, GridUnitType.Star);

    public SystemViewModel(Action close)
    {
        _close = close;
        OpenTaskManagerCommand = new RelayCommand(OpenTaskManager);
    }

    public IRelayCommand OpenTaskManagerCommand { get; }

    /// <summary>The charts' width once laid out; the points are drawn to it.</summary>
    public void SetChartWidth(double width)
    {
        if (width <= 0 || Math.Abs(width - _chartWidth) < 0.5) return;
        _chartWidth = width;
        if (_last is { } last) Apply(last);
    }

    public void Apply(StatsSnapshot s)
    {
        _last = s;
        (CpuText, CpuTooltip) = Percent(s.Cpu.Value?.Percent, s.Cpu.Error);
        (RamText, RamTooltip) = Percent(s.Memory.Value?.Percent, s.Memory.Error);
        (GpuText, GpuTooltip) = Percent(s.Gpu.Value?.Percent, s.Gpu.Error);
        DownText = s.Network.Value is { } net ? StatText.Rate(net.DownBytesPerSecond) : "—";
        DownTooltip = s.Network.Error;
        CpuPoints = Chart(s.History.Cpu, 100);
        RamPoints = Chart(s.History.Ram, 100);
        GpuPoints = Chart(s.History.Gpu, 100);
        DownPoints = Chart(s.History.Down, Sparkline.DownloadMax(s.History.Down));

        if (s.Disk.Value is { } disk)
        {
            DiskTitle = "Disk " + disk.Drive;
            DiskText = StatText.DiskFree(disk.FreeBytes);
            DiskTooltip = null;
            var used = disk.TotalBytes == 0 ? 0 : 1 - (double)disk.FreeBytes / disk.TotalBytes;
            DiskUsed = new GridLength(used, GridUnitType.Star);
            DiskFree = new GridLength(1 - used, GridUnitType.Star);
        }
        else if (s.Disk.Error is { } error)
        {
            DiskText = "—";
            DiskTooltip = error;
        }
    }

    private static (string Text, string? Tooltip) Percent(double? value, string? error) =>
        value is { } v ? (StatText.Percent(v), null) : ("—", error);

    private PointCollection Chart(IReadOnlyList<double> values, double max)
    {
        var points = new PointCollection(values.Count);
        foreach (var (x, y) in Sparkline.Points(values, max, _chartWidth, ChartHeight)) points.Add(new Point(x, y));
        points.Freeze();
        return points;
    }

    private void OpenTaskManager()
    {
        _close();
        SystemActions.TaskManager();
    }
}
