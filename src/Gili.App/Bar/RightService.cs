using System.Net.Http;
using System.Text.Json;
using System.Windows.Threading;
using Gili.Core.Diagnostics;
using Gili.Core.Modules;
using Gili.Platform.Modules;
using Gili.Platform.Shell;

namespace Gili.App.Bar;

/// <summary>
/// The readings behind the right of the bar (PRD 6.4): weather every 30 minutes (backing off on failure, PRD 11),
/// Wi‑Fi on connection events and every 10 s, Bluetooth and volume from system events. Changed fires on the UI thread.
/// </summary>
public sealed class RightService : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly SettingsHub _settings;
    private readonly FileLog _log;
    private readonly WifiMonitor _wifi = new();
    private readonly BluetoothMonitor _bluetooth;
    private readonly AudioMonitor _audio = new();
    private readonly RadioControl _radios = new();
    private readonly BrightnessControl _brightness = new();
    private readonly DispatcherTimer _weatherTimer = new();
    private readonly DispatcherTimer _wifiTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private readonly CancellationTokenSource _stop = new();
    private int _weatherFailures;

    public RightService(Dispatcher dispatcher, SettingsHub settings, FileLog log, BluetoothMonitor bluetooth)
    {
        _bluetooth = bluetooth;
        _dispatcher = dispatcher;
        _settings = settings;
        _log = log;
        _wifi.Changed += () => _dispatcher.InvokeAsync(ReadWifi);
        _bluetooth.Changed += () => _dispatcher.InvokeAsync(() =>
        {
            Changed?.Invoke();
            BluetoothChanged?.Invoke();
        });
        _audio.Changed += () => _dispatcher.InvokeAsync(() => Changed?.Invoke());
        _radios.Changed += () => _dispatcher.InvokeAsync(() =>
        {
            ReadWifi();
            RadiosChanged?.Invoke();
        });
        _wifi.ListChanged += () => _dispatcher.InvokeAsync(() => WifiListChanged?.Invoke());
        _wifi.ConnectFinished += ok => _dispatcher.InvokeAsync(() => WifiConnectFinished?.Invoke(ok));
        _wifiTimer.Tick += (_, _) => ReadWifi();
        _weatherTimer.Tick += (_, _) => _ = RefreshWeatherAsync();
        Brightness = BrightnessReader.Read();
    }

    public event Action? Changed;

    /// <summary>A Bluetooth device connected or disconnected, or the radio switched; on the UI thread.</summary>
    public event Action? BluetoothChanged;

    /// <summary>The Wi‑Fi or Bluetooth radio switched; on the UI thread.</summary>
    public event Action? RadiosChanged;

    /// <summary>A Wi‑Fi scan finished, for the open Wi‑Fi island; on the UI thread.</summary>
    public event Action? WifiListChanged;

    /// <summary>A Wi‑Fi connection finished (true when connected); on the UI thread.</summary>
    public event Action<bool>? WifiConnectFinished;

    public WeatherReading? Weather { get; private set; }

    /// <summary>The last reading is older than the latest attempt: shown grey (F-RIGHT-05).</summary>
    public bool WeatherStale { get; private set; }

    public WifiState? Wifi { get; private set; }

    public BluetoothState? Bluetooth => _bluetooth.Current;

    public (int Percent, bool Muted)? Volume => _audio.Current;

    public void Unmute() => _audio.Unmute();

    /// <summary>Null when the display has no brightness control, so the module hides.</summary>
    public int? Brightness { get; private set; }

    public string City => _settings.Current.WeatherCity;

    /// <summary>The Wi‑Fi radio's switch, null without one; Bluetooth's comes from <see cref="Bluetooth"/>.</summary>
    public bool? WifiRadioOn => _radios.WifiOn ?? Wifi?.RadioOn;

    public bool? BluetoothRadioOn => _radios.BluetoothOn ?? Bluetooth?.On;

    /// <summary>The Airplane mode switch (plan 3b): Windows' flag, or both radios off.</summary>
    public bool AirplaneOn => AirplaneRules.IsOn(RadioControl.SystemAirplane, _radios.WifiOn, _radios.BluetoothOn);

    public void SetVolume(int percent) => _audio.SetVolume(percent);

    public void SetMute(bool muted) => _audio.SetMute(muted);

    /// <summary>Moves the display's brightness; the module's tooltip follows at once.</summary>
    public void SetBrightness(int percent)
    {
        Brightness = Math.Clamp(percent, 0, 100);
        _brightness.Set(Brightness.Value);
    }

    public IReadOnlyList<WifiRow> WifiNetworks() => Core.Modules.WifiNetworks.Order(_wifi.Networks());

    public void ScanWifi() => _wifi.Scan();

    public bool ConnectWifi(string profile) => _wifi.Connect(profile);

    private static readonly TimeSpan PairedTimeout = TimeSpan.FromSeconds(5);

    /// <summary>The paired devices for the Bluetooth island; a failed or stuck read is logged and lists none (PRD 11).</summary>
    public async Task<IReadOnlyList<PairedDevice>> PairedAsync()
    {
        try
        {
            var read = _bluetooth.PairedAsync();
            if (await Task.WhenAny(read, Task.Delay(PairedTimeout)) == read) return await read;
            _log.Write("Paired Bluetooth devices did not answer in 5 s");
            return [];
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.Write("Paired Bluetooth devices are unavailable", ex);
            return [];
        }
    }

    public static Task<bool> ConnectBluetoothAsync(PairedDevice device, bool connect) => BluetoothMonitor.ConnectAsync(device, connect);

    public Task<bool> SetWifiRadioAsync(bool on) => _radios.SetWifiAsync(on);

    public Task<bool> SetBluetoothRadioAsync(bool on) => _radios.SetBluetoothAsync(on);

    /// <summary>
    /// Switches every radio; when Windows refuses to switch them back on because its own airplane flag is set, its
    /// Airplane mode page opens instead (plan 3b).
    /// </summary>
    public async Task SetAirplaneAsync(bool on)
    {
        if (!await _radios.SetAirplaneAsync(on) && !on) SystemActions.OpenSettings("ms-settings:network-airplanemode");
    }

    public void Start()
    {
        ReadWifi();
        _wifiTimer.Start();
        _ = StartRadiosAsync();
        _ = StartBluetoothAsync();
        _ = RefreshWeatherAsync();
    }

    /// <summary>The Wi‑Fi signal stops being read while the screen is off or the bar is hidden (PRD 8.1).</summary>
    public void SetPaused(bool paused)
    {
        if (paused)
        {
            _wifiTimer.Stop();
            return;
        }
        ReadWifi();
        _wifiTimer.Start();
    }

    /// <summary>Brightness is read when its tooltip opens, not polled.</summary>
    public int? ReadBrightness() => Brightness = BrightnessReader.Read();

    public void Dispose()
    {
        _stop.Cancel();
        _weatherTimer.Stop();
        _wifiTimer.Stop();
        _wifi.Dispose();
        _bluetooth.Dispose();
        _audio.Dispose();
    }

    private void ReadWifi()
    {
        var wifi = _wifi.Read();
        if (wifi == Wifi) return;
        Wifi = wifi;
        Changed?.Invoke();
    }

    private async Task StartRadiosAsync()
    {
        try
        {
            await _radios.StartAsync();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            _log.Write("Radio switches are unavailable", ex);
        }
    }

    private async Task StartBluetoothAsync()
    {
        try
        {
            await _bluetooth.StartAsync();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            _log.Write("Bluetooth state is unavailable", ex);
        }
        Changed?.Invoke();
    }

    private async Task RefreshWeatherAsync()
    {
        _weatherTimer.Stop();
        var s = _settings.Current;
        try
        {
            Weather = await WeatherClient.GetAsync(s.WeatherLatitude, s.WeatherLongitude, _stop.Token);
            WeatherStale = false;
            _weatherFailures = 0;
            _weatherTimer.Interval = TimeSpan.FromMinutes(30);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            if (_stop.IsCancellationRequested) return;
            WeatherStale = Weather is not null;
            _weatherFailures++;
            _weatherTimer.Interval = ProviderRetry.Delay(_weatherFailures);
        }
        _weatherTimer.Start();
        Changed?.Invoke();
    }
}
