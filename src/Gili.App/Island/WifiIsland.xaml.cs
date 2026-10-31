using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Gili.App.Bar;
using Gili.Core.Modules;
using Gili.Platform.Shell;

namespace Gili.App.Island;

/// <summary>
/// The Wi‑Fi island (PRD 6.4): the radio and Airplane mode switches, and Windows' network list, read when the island
/// opens and again when the scan it asks for finishes. A saved network connects at a click; any other opens Windows'
/// network list, since Gili never handles passwords.
/// </summary>
public partial class WifiIsland : UserControl, IControlIsland
{
    private const string More = "more";

    private static readonly Brush TileOff = Frozen(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF));
    private static readonly Brush TileOn = Frozen(Color.FromRgb(0x0A, 0x84, 0xFF));

    private readonly RightService _right;
    private readonly IslandController _island;
    private readonly DispatcherTimer _connectTimeout = new() { Interval = TimeSpan.FromSeconds(15) };
    private bool _open;
    private bool _rendering;
    private bool _wasOn;
    private string? _connecting;

    public WifiIsland(RightService right, IslandController island)
    {
        _right = right;
        _island = island;
        InitializeComponent();
        right.RadiosChanged += () =>
        {
            if (_open) Refresh(scanIfTurnedOn: true);
        };
        right.WifiListChanged += () =>
        {
            if (_open) Refresh();
        };
        right.WifiConnectFinished += _ =>
        {
            _connecting = null;
            _connectTimeout.Stop();
            if (_open) Refresh();
        };
        _connectTimeout.Tick += (_, _) =>
        {
            _connectTimeout.Stop();
            _connecting = null;
            if (_open) Refresh();
        };
    }

    public event Action? Resized;

    public void Shown()
    {
        _open = true;
        Refresh();
        if (_wasOn) _right.ScanWifi();
    }

    public void Hidden() => _open = false;

    private void Refresh(bool scanIfTurnedOn = false)
    {
        var airplane = _right.AirplaneOn;
        var radio = _right.WifiRadioOn;
        var on = radio == true && !airplane;
        if (scanIfTurnedOn && on && !_wasOn) _right.ScanWifi();
        _wasOn = on;
        _rendering = true;
        WifiSwitch.IsChecked = on;
        AirSwitch.IsChecked = airplane;
        _rendering = false;
        WifiSwitch.IsEnabled = radio is not null;
        WifiTile.Background = on ? TileOn : TileOff;
        AirTile.Background = airplane ? TileOn : TileOff;

        Off.Text = radio is null ? "Not available" : airplane ? "Airplane mode is on" : "Wi‑Fi is off";
        Off.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        ListArea.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (on)
        {
            var wifi = (Geometry)FindResource("Icon.Wifi");
            var rows = _right.WifiNetworks()
                .Select(n => new ControlRowItem(
                    n.Ssid, n.Ssid == _connecting ? "Connecting…" : n.StatusText, wifi, n.Status == WifiStatus.Connected, n))
                .ToList();
            rows.Add(new ControlRowItem("More Wi‑Fi settings", "", (Geometry)FindResource("Icon.Plus"), false, More));
            Networks.ItemsSource = rows;
        }
        Resized?.Invoke();
    }

    private async void OnWifiSwitch(object sender, RoutedEventArgs e)
    {
        // Checked rather than Click, so a screen reader's toggle also switches the radio; not while showing the state.
        if (_rendering) return;
        var on = WifiSwitch.IsChecked == true;
        // Turning Wi‑Fi on leaves Airplane mode, as in Windows (mockup).
        if (on && _right.AirplaneOn) await _right.SetAirplaneAsync(false);
        else await _right.SetWifiRadioAsync(on);
        Refresh(scanIfTurnedOn: true);
    }

    private async void OnAirSwitch(object sender, RoutedEventArgs e)
    {
        // Checked rather than Click, so a screen reader's toggle also switches the radio; not while showing the state.
        if (_rendering) return;
        await _right.SetAirplaneAsync(AirSwitch.IsChecked == true);
        Refresh(scanIfTurnedOn: true);
    }

    private void OnRow(object sender, RoutedEventArgs e)
    {
        switch ((e.OriginalSource as FrameworkElement)?.DataContext)
        {
            case ControlRowItem { Tag: More }:
                Leave("ms-settings:network-wifi");
                break;
            case ControlRowItem { Tag: WifiRow { Status: WifiStatus.Connected } }:
                break;
            case ControlRowItem { Tag: WifiRow { Profile: { } profile } row }:
                if (!_right.ConnectWifi(profile)) return;
                _connecting = row.Ssid;
                _connectTimeout.Start();
                Refresh();
                break;
            case ControlRowItem { Tag: WifiRow }:
                Leave("ms-availablenetworks:");
                break;
        }
    }

    /// <summary>Links to Windows close the island first, like the Power island's buttons.</summary>
    private void Leave(string uri)
    {
        _island.Close();
        SystemActions.OpenSettings(uri);
    }

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
