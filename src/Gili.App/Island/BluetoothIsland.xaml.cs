using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Gili.App.Bar;
using Gili.Core.Devices;
using Gili.Core.Modules;
using Gili.Platform.Shell;

namespace Gili.App.Island;

/// <summary>
/// The Bluetooth island (PRD 6.4): the radio switch and the paired devices, read when the island opens and whenever a
/// device connects or disconnects meanwhile. An audio device connects or disconnects at a click; for any other the
/// click opens Windows' Bluetooth settings, as Windows offers no public way to connect it.
/// </summary>
public partial class BluetoothIsland : UserControl, IControlIsland
{
    private const string Add = "add";
    private static readonly TimeSpan ConnectWait = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan FailureShown = TimeSpan.FromSeconds(3);

    private static readonly Brush TileOff = Frozen(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF));
    private static readonly Brush TileOn = Frozen(Color.FromRgb(0x0A, 0x84, 0xFF));

    private readonly RightService _right;
    private readonly IslandController _island;
    private readonly DispatcherTimer _timer = new();
    private IReadOnlyList<PairedDevice> _devices = [];
    private bool _open;
    private bool _rendering;
    private bool _reading;
    private bool _readAgain;
    private string? _connecting;
    private string? _failed;

    public BluetoothIsland(RightService right, IslandController island)
    {
        _right = right;
        _island = island;
        InitializeComponent();
        right.BluetoothChanged += () =>
        {
            if (_open) _ = ReadAsync();
        };
        right.RadiosChanged += () =>
        {
            if (_open) _ = ReadAsync();
        };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            // The device never came: say so for 3 s, then the row reads Paired again.
            if (_connecting is not null)
            {
                _failed = _connecting;
                _connecting = null;
                _timer.Interval = FailureShown;
                _timer.Start();
            }
            else
            {
                _failed = null;
            }
            Render();
        };
    }

    public event Action? Resized;

    public void Shown()
    {
        _open = true;
        Render();
        _ = ReadAsync();
    }

    public void Hidden() => _open = false;

    /// <summary>Off while Airplane mode holds the radio off; null without a Bluetooth radio.</summary>
    private bool? RadioOn => _right.BluetoothRadioOn is { } on ? on && !_right.AirplaneOn : null;

    /// <summary>One read at a time; a change during it reads once more after.</summary>
    private async Task ReadAsync()
    {
        if (_reading)
        {
            _readAgain = true;
            return;
        }
        _reading = true;
        do
        {
            _readAgain = false;
            _devices = RadioOn == true ? await _right.PairedAsync() : [];
        }
        while (_readAgain);
        _reading = false;
        if (_connecting is { } id && _devices.FirstOrDefault(d => d.Id == id) is { Connected: true })
        {
            _connecting = null;
            _timer.Stop();
        }
        if (_open) Render();
    }

    private void Render()
    {
        var on = RadioOn;
        _rendering = true;
        RadioSwitch.IsChecked = on == true;
        _rendering = false;
        RadioSwitch.IsEnabled = on is not null;
        Tile.Background = on == true ? TileOn : TileOff;
        Off.Text = on is null ? "Not available" : "Bluetooth is off";
        Off.Visibility = on == true ? Visibility.Collapsed : Visibility.Visible;
        ListArea.Visibility = on == true ? Visibility.Visible : Visibility.Collapsed;
        if (on == true)
        {
            var rows = _devices
                .Select(d => new ControlRowItem(d.Name, Status(d), Glyph(d.Kind), d.Connected, d))
                .ToList();
            rows.Add(new ControlRowItem("Add device", "", (Geometry)FindResource("Icon.Plus"), false, Add));
            Devices.ItemsSource = rows;
        }
        Resized?.Invoke();
    }

    private string Status(PairedDevice device) =>
        device.Id == _connecting ? "Connecting…"
        : device.Id == _failed ? "Couldn't connect"
        : BluetoothList.Status(device);

    private Geometry Glyph(BluetoothKind kind) => (Geometry)FindResource(kind switch
    {
        BluetoothKind.Audio => "Icon.Headphones",
        BluetoothKind.Mouse => "Icon.Mouse",
        BluetoothKind.Keyboard => "Icon.Keyboard",
        BluetoothKind.Gamepad => "Icon.Gamepad",
        BluetoothKind.Phone => "Icon.Phone",
        _ => "Icon.Bluetooth",
    });

    private async void OnSwitch(object sender, RoutedEventArgs e)
    {
        // Checked rather than Click, so a screen reader's toggle also switches the radio; not while showing the state.
        if (_rendering) return;
        var on = RadioSwitch.IsChecked == true;
        // Turning Bluetooth on leaves Airplane mode, as in Windows (mockup).
        if (on && _right.AirplaneOn) await _right.SetAirplaneAsync(false);
        else await _right.SetBluetoothRadioAsync(on);
        await ReadAsync();
    }

    private async void OnRow(object sender, RoutedEventArgs e)
    {
        switch ((e.OriginalSource as FrameworkElement)?.DataContext)
        {
            case ControlRowItem { Tag: Add }:
            case ControlRowItem { Tag: PairedDevice { CanConnect: false } }:
                Leave();
                break;
            case ControlRowItem { Tag: PairedDevice device }:
                if (device.Id == _connecting) return;
                _failed = null;
                _timer.Stop();
                if (!device.Connected)
                {
                    _connecting = device.Id;
                    _timer.Interval = ConnectWait;
                    _timer.Start();
                    Render();
                }
                if (!await RightService.ConnectBluetoothAsync(device, !device.Connected) && !device.Connected && _connecting == device.Id)
                {
                    // Windows did not take the request at all: fail now rather than after the wait.
                    _timer.Stop();
                    _timer.Interval = TimeSpan.FromMilliseconds(1);
                    _timer.Start();
                }
                break;
        }
    }

    private void Leave()
    {
        _island.Close();
        SystemActions.OpenSettings("ms-settings:bluetooth");
    }

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
