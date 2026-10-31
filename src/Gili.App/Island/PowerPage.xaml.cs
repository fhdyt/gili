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
        island.UpdatesChanged += ShowUpdates;
        Refresh();
        ShowUpdates();
    }

    /// <summary>The update buttons come and go with Windows' waiting updates, and the island fits them.</summary>
    public event Action? Resized;

    public void Shown()
    {
        Refresh();
        _island.RefreshUpdates();
    }

    private void ShowUpdates()
    {
        var shown = _island.UpdatesPending ? Visibility.Visible : Visibility.Collapsed;
        if (UpdateShutDown.Visibility == shown) return;
        UpdateShutDown.Visibility = UpdateRestart.Visibility = shown;
        Resized?.Invoke();
    }

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

    private void OnUpdateShutDown(object sender, RoutedEventArgs e) => Run(() => Update(PendingUpdates.UpdateAndShutDown));

    private void OnUpdateRestart(object sender, RoutedEventArgs e) => Run(() => Update(PendingUpdates.UpdateAndRestart));

    /// <summary>When Windows refuses the update variant, its Windows Update page opens instead (PRD 12).</summary>
    private static void Update(Func<bool> shutdown)
    {
        if (!shutdown()) SystemActions.OpenSettings("ms-settings:windowsupdate");
    }

    private void OnQuickSettings(object sender, RoutedEventArgs e) => Run(SystemActions.QuickSettings);

    private void OnAllSettings(object sender, RoutedEventArgs e) => Run(() => SystemActions.OpenSettings("ms-settings:"));
}
