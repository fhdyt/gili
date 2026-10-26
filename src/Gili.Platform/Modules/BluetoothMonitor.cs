using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.Radios;

namespace Gili.Platform.Modules;

/// <summary>Bluetooth on or off, and the name of a connected device, if any.</summary>
public sealed record BluetoothState(bool On, string? Connected);

/// <summary>
/// The Bluetooth radio and connected devices from system events, never polled (PRD 6.4): the radio's StateChanged and
/// device watchers for connected classic and LE devices.
/// </summary>
public sealed class BluetoothMonitor : IDisposable
{
    private readonly Dictionary<string, string> _connected = [];
    private readonly List<DeviceWatcher> _watchers = [];
    private Radio? _radio;

    /// <summary>Null until started, or when there is no Bluetooth radio.</summary>
    public BluetoothState? Current { get; private set; }

    /// <summary>Raised on a background thread.</summary>
    public event Action? Changed;

    public async Task StartAsync()
    {
        var radios = await Radio.GetRadiosAsync();
        _radio = radios.FirstOrDefault(r => r.Kind == RadioKind.Bluetooth);
        if (_radio is null) return;
        _radio.StateChanged += (_, _) => Publish();
        Watch(BluetoothDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected));
        Watch(BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected));
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

    private void Watch(string selector)
    {
        var watcher = DeviceInformation.CreateWatcher(selector);
        watcher.Added += (_, device) => Set(device.Id, device.Name);
        watcher.Removed += (_, update) => Set(update.Id, null);
        watcher.Updated += (_, _) => { };
        watcher.Start();
        _watchers.Add(watcher);
    }

    private void Set(string id, string? name)
    {
        lock (_connected)
        {
            if (name is null) _connected.Remove(id);
            else _connected[id] = name;
        }
        Publish();
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
