using CommunityToolkit.Mvvm.ComponentModel;
using Gili.Core.Settings;

namespace Gili.App.Settings;

/// <summary>Phase 1 part of F-BAR-08: clock format, material, bar and island height, autostart. Every change applies at once.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsHub _hub;
    private bool _loading;

    [ObservableProperty] private double _barOpacity;
    [ObservableProperty] private GlassLevel _glass;
    [ObservableProperty] private double _barHeight;
    [ObservableProperty] private double _islandHeight;
    [ObservableProperty] private double _maxIslandHeight;
    [ObservableProperty] private bool _use24HourClock;
    [ObservableProperty] private bool _autostart;

    public SettingsViewModel(SettingsHub hub, bool transparencyEnabled, bool installedBuild)
    {
        _hub = hub;
        TransparencyOff = !transparencyEnabled;
        AutostartNeedsInstall = !installedBuild;
        Load(hub.Current);
    }

    public GlassLevel[] GlassLevels { get; } = Enum.GetValues<GlassLevel>();
    public bool TransparencyOff { get; }
    public bool AutostartNeedsInstall { get; }

    partial void OnBarOpacityChanged(double value) => Push();
    partial void OnGlassChanged(GlassLevel value) => Push();
    partial void OnBarHeightChanged(double value) => Push();
    partial void OnIslandHeightChanged(double value) => Push();
    partial void OnUse24HourClockChanged(bool value) => Push();
    partial void OnAutostartChanged(bool value) => Push();

    private void Load(AppSettings settings)
    {
        _loading = true;
        BarOpacity = settings.BarOpacity;
        Glass = settings.Glass;
        BarHeight = settings.BarHeight;
        MaxIslandHeight = AppSettings.MaxIslandHeight(settings.BarHeight);
        IslandHeight = settings.IslandHeight;
        Use24HourClock = settings.Use24HourClock;
        Autostart = settings.Autostart;
        _loading = false;
    }

    private void Push()
    {
        if (_loading) return;
        _hub.Update(s => s with
        {
            BarOpacity = (int)Math.Round(BarOpacity),
            Glass = Glass,
            BarHeight = (int)Math.Round(BarHeight),
            IslandHeight = (int)Math.Round(IslandHeight),
            Use24HourClock = Use24HourClock,
            Autostart = Autostart,
        });
        // Shows the clamped values, e.g. the island shrinking with a lower bar.
        Load(_hub.Current);
    }
}
