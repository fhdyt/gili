using System.Windows;
using System.Windows.Controls;
using Gili.Core.Modules;
using Gili.Platform.Power;
using Gili.Platform.Shell;

namespace Gili.App.Island;

public partial class PowerPage : UserControl, IControlIsland
{
    private readonly IslandController _island;

    public PowerPage(IslandController island)
    {
        _island = island;
        InitializeComponent();
        island.PowerChanged += Refresh;
        Refresh();
    }

    /// <summary>The buttons never change height.</summary>
    public event Action? Resized
    {
        add { }
        remove { }
    }

    public void Shown() => Refresh();

    public void Hidden()
    {
    }

    /// <summary>The battery line on the right of the title: empty without a battery (PRD 6.4).</summary>
    public void Refresh()
    {
        var power = _island.Power;
        Battery.Text = power is { HasBattery: true } p
            ? BatteryText.Line(p.Percent, p.Charging, p.PluggedIn ? null : PowerMonitor.RemainingDischargeTime())
            : "";
    }

    private void Run(Action action)
    {
        _island.Close();
        action();
    }

    private void OnSleep(object sender, RoutedEventArgs e) => Run(SystemActions.Sleep);

    private void OnShutDown(object sender, RoutedEventArgs e) => Run(SystemActions.ShutDown);

    private void OnRestart(object sender, RoutedEventArgs e) => Run(SystemActions.Restart);

    private void OnQuickSettings(object sender, RoutedEventArgs e) => Run(SystemActions.QuickSettings);

    private void OnAllSettings(object sender, RoutedEventArgs e) => Run(() => SystemActions.OpenSettings("ms-settings:"));
}
