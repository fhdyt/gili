using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Gili.Core.Formatting;
using Gili.Core.Layout;
using Gili.Core.Modules;
using Gili.Core.Settings;

namespace Gili.App.Bar;

public sealed partial class BarViewModel : ObservableObject
{
    private readonly NetworkToday _today = new();

    [ObservableProperty] private Brush _tint = Brushes.Transparent;
    [ObservableProperty] private double _moduleHeight = 26;
    [ObservableProperty] private string _cpuText = "—";
    [ObservableProperty] private string? _cpuTooltip;
    [ObservableProperty] private string _ramText = "—";
    [ObservableProperty] private string? _ramTooltip;
    [ObservableProperty] private string _upText = "↑ —";
    [ObservableProperty] private string _downText = "↓ —";
    [ObservableProperty] private string? _netTooltip;
    [ObservableProperty] private bool _hasWeather;
    [ObservableProperty] private Geometry? _weatherGlyph;
    [ObservableProperty] private string _weatherLabel = "";
    [ObservableProperty] private string _weatherValue = "";
    [ObservableProperty] private string? _weatherTooltip;
    [ObservableProperty] private double _weatherOpacity = 1;
    [ObservableProperty] private bool _hasWifi;
    [ObservableProperty] private bool _wifiHasText;
    [ObservableProperty] private string _wifiLabel = "";
    [ObservableProperty] private string _wifiValue = "";
    [ObservableProperty] private string? _wifiTooltip;
    [ObservableProperty] private bool _hasBluetooth;
    [ObservableProperty] private bool _bluetoothConnected;
    [ObservableProperty] private string? _bluetoothTooltip;
    [ObservableProperty] private bool _hasVolume;
    [ObservableProperty] private Geometry? _volumeGlyph;
    [ObservableProperty] private string? _volumeTooltip;
    [ObservableProperty] private bool _hasBrightness;
    [ObservableProperty] private string? _brightnessTooltip;
    [ObservableProperty] private string _clockTime = "";
    [ObservableProperty] private string _clockDate = "";
    [ObservableProperty] private string _clockTooltip = "";

    /// <summary>The widest the time can be, laid under it so the bar never shifts as the digits tick (PRD 6.4).</summary>
    [ObservableProperty] private string _clockTemplate = "00:00:00";

    /// <summary>The right-hand readings as the mockup shows them (PRD 6.4).</summary>
    public void ApplyRight(RightService right, Func<string, Geometry> glyph)
    {
        HasWeather = right.Weather is not null;
        if (right.Weather is { } weather)
        {
            var (label, condition, icon) = WeatherText.For(weather.Code, weather.Day);
            WeatherGlyph = glyph("Icon." + (icon switch
            {
                WeatherIcon.Sun => "Sun",
                WeatherIcon.Moon => "Moon",
                _ => "Weather." + icon,
            }));
            WeatherLabel = label.ToUpperInvariant();
            WeatherValue = WeatherText.Temperature(weather.Temperature);
            WeatherTooltip = WeatherText.Tooltip(condition, weather.Max, weather.Min, right.City);
            // A stale reading stays, greyed out, until the next one arrives (F-RIGHT-05).
            WeatherOpacity = right.WeatherStale ? 0.5 : 1;
        }

        HasWifi = right.Wifi is not null;
        if (right.Wifi is { } wifi)
        {
            var on = wifi.RadioOn;
            WifiHasText = !wifi.NameDenied;
            WifiLabel = !on ? "WI‑FI" : wifi.Connected ? WifiText.Strength(wifi.Quality).ToUpperInvariant() : "WI‑FI";
            WifiValue = !on ? "Off" : wifi.Connected && wifi.Ssid is { } ssid ? WifiText.Name(ssid) : "—";
            WifiTooltip = !on ? "Wi‑Fi is off"
                : wifi.NameDenied ? "Allow location access for Gili to show the Wi‑Fi name (Settings > Privacy & security > Location)"
                : wifi.Connected ? $"{WifiText.Strength(wifi.Quality)} signal · {wifi.Ssid}"
                : "Wi‑Fi · Not connected";
        }

        HasBluetooth = right.Bluetooth is not null;
        if (right.Bluetooth is { } bluetooth)
        {
            BluetoothConnected = bluetooth.Connected is not null;
            BluetoothTooltip = "Bluetooth · " + (bluetooth.Connected ?? (bluetooth.On ? "On" : "Off"));
        }

        HasVolume = right.Volume is not null;
        if (right.Volume is { } volume)
        {
            VolumeGlyph = glyph("Icon.Volume." + VolumeText.Level(volume.Percent, volume.Muted));
            VolumeTooltip = VolumeText.Tooltip(volume.Percent, volume.Muted);
        }

        HasBrightness = right.Brightness is not null;
        BrightnessTooltip = right.Brightness is { } level ? $"Brightness {level}%" : null;
    }

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
        if (network is not null) _today.Add(network.UpBytes, network.DownBytes, DateTime.Now);
        NetTooltip = network is null ? stats.Network.Error : _today.Tooltip(network.Adapters);
    }
}
