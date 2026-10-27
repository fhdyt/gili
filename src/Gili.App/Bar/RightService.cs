using System.Net.Http;
using System.Text.Json;
using System.Windows.Threading;
using Gili.Core.Diagnostics;
using Gili.Core.Modules;
using Gili.Platform.Modules;

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
    private readonly BluetoothMonitor _bluetooth = new();
    private readonly AudioMonitor _audio = new();
    private readonly DispatcherTimer _weatherTimer = new();
    private readonly DispatcherTimer _wifiTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private readonly CancellationTokenSource _stop = new();
    private int _weatherFailures;

    public RightService(Dispatcher dispatcher, SettingsHub settings, FileLog log)
    {
        _dispatcher = dispatcher;
        _settings = settings;
        _log = log;
        _wifi.Changed += () => _dispatcher.InvokeAsync(ReadWifi);
        _bluetooth.Changed += () => _dispatcher.InvokeAsync(() => Changed?.Invoke());
        _audio.Changed += () => _dispatcher.InvokeAsync(() => Changed?.Invoke());
        _wifiTimer.Tick += (_, _) => ReadWifi();
        _weatherTimer.Tick += (_, _) => _ = RefreshWeatherAsync();
        Brightness = BrightnessReader.Read();
    }

    public event Action? Changed;

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

    public void Start()
    {
        ReadWifi();
        _wifiTimer.Start();
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
