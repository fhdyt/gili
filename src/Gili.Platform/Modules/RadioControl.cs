using Microsoft.Win32;
using Windows.Devices.Radios;

namespace Gili.Platform.Modules;

/// <summary>
/// The Wi‑Fi and Bluetooth radios for the control islands' switches (F-RIGHT-10), through the public radio API, and
/// Windows' own airplane flag, read from the registry. Airplane mode here switches every radio (plan 3b): the API
/// cannot set Windows' flag.
/// </summary>
public sealed class RadioControl
{
    private const string AirplaneKey = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\RadioManagement\SystemRadioState";

    private IReadOnlyList<Radio> _radios = [];

    /// <summary>Raised on a background thread when a radio turns on or off.</summary>
    public event Action? Changed;

    public bool? WifiOn => State(RadioKind.WiFi);

    public bool? BluetoothOn => State(RadioKind.Bluetooth);

    /// <summary>Windows' airplane flag; read each time, as nothing reports its change.</summary>
    public static bool SystemAirplane => Registry.GetValue(AirplaneKey, "", 0) is int flag && flag == 1;

    public async Task StartAsync()
    {
        if (await Radio.RequestAccessAsync() != RadioAccessStatus.Allowed) return;
        _radios = await Radio.GetRadiosAsync();
        foreach (var radio in _radios) radio.StateChanged += (_, _) => Changed?.Invoke();
        Changed?.Invoke();
    }

    /// <summary>False when Windows refused, for instance while its airplane flag is on.</summary>
    public Task<bool> SetWifiAsync(bool on) => SetAsync(on, RadioKind.WiFi);

    public Task<bool> SetBluetoothAsync(bool on) => SetAsync(on, RadioKind.Bluetooth);

    /// <summary>On switches every radio off; off switches Wi‑Fi and Bluetooth back on.</summary>
    public async Task<bool> SetAirplaneAsync(bool on)
    {
        var ok = await SetAsync(!on, RadioKind.WiFi);
        return await SetAsync(!on, RadioKind.Bluetooth) && ok;
    }

    private bool? State(RadioKind kind) =>
        _radios.FirstOrDefault(r => r.Kind == kind) is { } radio ? radio.State == RadioState.On : null;

    private async Task<bool> SetAsync(bool on, RadioKind kind)
    {
        var ok = true;
        foreach (var radio in _radios.Where(r => r.Kind == kind))
        {
            try
            {
                if (await radio.SetStateAsync(on ? RadioState.On : RadioState.Off) != RadioAccessStatus.Allowed) ok = false;
            }
            catch (Exception e) when (e is UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
            {
                ok = false;
            }
        }
        return ok;
    }
}
