using Gili.Core.Devices;
using Gili.Core.Modules;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.Radios;

namespace Gili.Platform.Modules;

/// <summary>Bluetooth on or off, and the name of a connected device, if any.</summary>
public sealed record BluetoothState(bool On, string? Connected);

/// <summary>
/// The Bluetooth radio and connected devices from system events, never polled (PRD 6.4): the radio's StateChanged and
/// device watchers for connected classic and LE devices. Devices that connect or disconnect after each watcher's first
/// enumeration are reported to the island (F-ISL-18); the ones already connected at start are not.
/// </summary>
public sealed class BluetoothMonitor : IDisposable
{
    /// <summary>DEVPKEY_Bluetooth_Battery: the charge a device reports, 0–100.</summary>
    private const string BatteryKey = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";

    private const string IsConnectedKey = "System.Devices.Aep.IsConnected";
    private const string ContainerKey = "System.Devices.Aep.ContainerId";
    private const string MajorKey = "System.Devices.Aep.Bluetooth.Cod.Major";
    private const string MinorKey = "System.Devices.Aep.Bluetooth.Cod.Minor";
    private const string AppearanceKey = "System.Devices.Aep.Bluetooth.Le.Appearance";

    private readonly Dictionary<string, string> _connected = [];
    private readonly List<DeviceWatcher> _watchers = [];
    private Radio? _radio;
    private bool _radioOn;

    /// <summary>Null until started, or when there is no Bluetooth radio.</summary>
    public BluetoothState? Current { get; private set; }

    /// <summary>Raised on a background thread.</summary>
    public event Action? Changed;

    /// <summary>A device connected or disconnected after Gili started; raised on a background thread.</summary>
    public event Action<BluetoothChange>? DeviceChanged;

    public async Task StartAsync()
    {
        var radios = await Radio.GetRadiosAsync();
        _radio = radios.FirstOrDefault(r => r.Kind == RadioKind.Bluetooth);
        if (_radio is null) return;
        _radioOn = _radio.State == RadioState.On;
        _radio.StateChanged += (_, _) =>
        {
            _radioOn = _radio.State == RadioState.On;
            Publish();
        };
        Watch(BluetoothDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected), lowEnergy: false);
        Watch(BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected), lowEnergy: true);
        Publish();
    }

    public void Dispose()
    {
        foreach (var watcher in _watchers)
        {
            if (watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted) watcher.Stop();
        }
        _watchers.Clear();
    }

    private void Watch(string selector, bool lowEnergy)
    {
        var watcher = DeviceInformation.CreateWatcher(selector, [BatteryKey]);
        var started = false;
        watcher.EnumerationCompleted += (_, _) => started = true;
        watcher.Added += (source, device) =>
        {
            Set(device.Id, device.Name);
            if (started) _ = ReportAsync(device.Id, device.Name, Battery(device.Properties), lowEnergy, connected: true);
        };
        watcher.Removed += (source, update) =>
        {
            var name = Set(update.Id, null);
            // Switching the radio off drops every device at once; that is not a disconnect worth a notice each.
            if (started && _radioOn && name is not null) _ = ReportAsync(update.Id, name, null, lowEnergy, connected: false);
        };
        watcher.Updated += (_, _) => { };
        watcher.Start();
        _watchers.Add(watcher);
    }

    /// <summary>
    /// The paired devices for the Bluetooth island (F-RIGHT-10), read when it opens: one per device (a phone pairs over
    /// classic and LE), with its kind, whether it is connected, the charge it reports, and whether Gili can connect it
    /// itself. The ID is the device's container, which its audio endpoints share. The device-interface selectors are used
    /// rather than association-endpoint queries, which were seen to hang for good on the development laptop (plan 3b).
    /// </summary>
    public async Task<IReadOnlyList<PairedDevice>> PairedAsync()
    {
        var audio = await Task.Run(AudioDevices.BluetoothAudio);
        string[] keys = [IsConnectedKey, ContainerKey, MajorKey, MinorKey, AppearanceKey, BatteryKey];
        var devices = new Dictionary<string, PairedDevice>();
        foreach (var lowEnergy in new[] { false, true })
        {
            var selector = lowEnergy
                ? BluetoothLEDevice.GetDeviceSelectorFromPairingState(true)
                : BluetoothDevice.GetDeviceSelectorFromPairingState(true);
            foreach (var device in await DeviceInformation.FindAllAsync(selector, keys))
            {
                var container = device.Properties.TryGetValue(ContainerKey, out var c) && c is Guid g ? g : (Guid?)null;
                var id = container?.ToString() ?? device.Id;
                var connected = device.Properties.TryGetValue(IsConnectedKey, out var on) && on is true;
                var battery = Battery(device.Properties);
                if (devices.TryGetValue(id, out var known))
                {
                    devices[id] = known with { Connected = known.Connected || connected, Battery = known.Battery ?? battery };
                    continue;
                }
                var kind = lowEnergy
                    ? BluetoothRules.KindFromAppearance((ushort)Number(device.Properties, AppearanceKey))
                    : BluetoothRules.KindFromClass((Number(device.Properties, MajorKey) << 8) | (Number(device.Properties, MinorKey) << 2));
                var canConnect = container is { } box && audio.Contains(box);
                devices[id] = new PairedDevice(id, device.Name, canConnect ? BluetoothKind.Audio : kind, connected, battery, canConnect);
            }
        }
        return BluetoothList.Order(devices.Values);
    }

    /// <summary>Asks an audio device to connect or disconnect; false when Windows did not take the request.</summary>
    public static Task<bool> ConnectAsync(PairedDevice device, bool connect) =>
        Guid.TryParse(device.Id, out var container)
            ? Task.Run(() => AudioDevices.ConnectBluetooth(container, connect))
            : Task.FromResult(false);

    private static uint Number(IReadOnlyDictionary<string, object> properties, string key) =>
        properties.TryGetValue(key, out var value) && value is not null ? Convert.ToUInt32(value, System.Globalization.CultureInfo.InvariantCulture) : 0;

    private async Task ReportAsync(string id, string name, int? battery, bool lowEnergy, bool connected)
    {
        var kind = BluetoothKind.Other;
        try
        {
            if (lowEnergy)
            {
                using var device = await BluetoothLEDevice.FromIdAsync(id);
                if (device is not null) kind = BluetoothRules.KindFromAppearance(device.Appearance.RawValue);
            }
            else
            {
                using var device = await BluetoothDevice.FromIdAsync(id);
                if (device is not null) kind = BluetoothRules.KindFromClass(device.ClassOfDevice.RawValue);
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // A device gone before it could be asked: the plain Bluetooth icon will do.
        }
        DeviceChanged?.Invoke(new BluetoothChange(id, name, kind, connected, battery));
    }

    private static int? Battery(IReadOnlyDictionary<string, object> properties) =>
        properties.TryGetValue(BatteryKey, out var value) && value is byte percent && percent <= 100 ? percent : null;

    /// <summary>Adds or removes a device; returns the name it had.</summary>
    private string? Set(string id, string? name)
    {
        string? had;
        lock (_connected)
        {
            _connected.TryGetValue(id, out had);
            if (name is null) _connected.Remove(id);
            else _connected[id] = name;
        }
        Publish();
        return had;
    }

    private void Publish()
    {
        if (_radio is null) return;
        string? first;
        lock (_connected)
        {
            first = _connected.Values.FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
        }
        var on = _radio.State == RadioState.On;
        Current = new BluetoothState(on, on ? first : null);
        Changed?.Invoke();
    }
}
