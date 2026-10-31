using System.Globalization;
using Gili.Core.Devices;

namespace Gili.Core.Modules;

/// <summary>One entry of Windows' Wi‑Fi list: a network can show once per access point band.</summary>
public sealed record WifiNetwork(string Ssid, int Quality, bool Secured, bool HasProfile, bool Connected, string? Profile);

public enum WifiStatus { Connected, Saved, Secured, Open }

/// <summary>A row of the Wi‑Fi control island; <see cref="Profile"/> is what a click connects with.</summary>
public sealed record WifiRow(string Ssid, WifiStatus Status, string? Profile)
{
    public string StatusText => Status.ToString();
}

/// <summary>The Wi‑Fi island's list (PRD 6.4): one row per name, hidden networks left out, the connected one first.</summary>
public static class WifiNetworks
{
    public static IReadOnlyList<WifiRow> Order(IEnumerable<WifiNetwork> networks) =>
    [
        .. networks
            .Where(n => !string.IsNullOrWhiteSpace(n.Ssid))
            .GroupBy(n => n.Ssid, StringComparer.Ordinal)
            .Select(g =>
            {
                var best = g.OrderByDescending(n => n.Connected).ThenByDescending(n => n.HasProfile).ThenByDescending(n => n.Quality).First();
                var profile = g.Select(n => n.Profile).FirstOrDefault(p => p is not null);
                var status = g.Any(n => n.Connected) ? WifiStatus.Connected
                    : profile is not null ? WifiStatus.Saved
                    : best.Secured ? WifiStatus.Secured
                    : WifiStatus.Open;
                return (Row: new WifiRow(g.Key, status, profile), Quality: g.Max(n => n.Quality));
            })
            .OrderByDescending(r => r.Row.Status == WifiStatus.Connected)
            .ThenByDescending(r => r.Quality)
            .ThenBy(r => r.Row.Ssid, StringComparer.CurrentCultureIgnoreCase)
            .Select(r => r.Row),
    ];
}

/// <summary>
/// A paired Bluetooth device in the Bluetooth island. <see cref="CanConnect"/> is false for devices Gili cannot connect
/// itself (only audio devices can be), whose click opens Windows' Bluetooth settings instead.
/// </summary>
public sealed record PairedDevice(string Id, string Name, BluetoothKind Kind, bool Connected, int? Battery, bool CanConnect);

public static class BluetoothList
{
    public const string Unnamed = "Bluetooth device";

    public static IReadOnlyList<PairedDevice> Order(IEnumerable<PairedDevice> devices) =>
    [
        .. devices
            .Select(d => string.IsNullOrWhiteSpace(d.Name) ? d with { Name = Unnamed } : d with { Name = d.Name.Trim() })
            .OrderByDescending(d => d.Connected)
            .ThenBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase),
    ];

    /// <summary>"Connected · 80%", "Connected" or "Paired".</summary>
    public static string Status(PairedDevice device) =>
        !device.Connected ? "Paired"
        : device.Battery is >= 0 and <= 100 ? "Connected · " + device.Battery.Value.ToString(CultureInfo.InvariantCulture) + "%"
        : "Connected";
}

public static class ControlSliders
{
    /// <summary>Scrolling over a slider moves it 2 points per notch direction (PRD 6.4), within 0–100.</summary>
    public static int Wheel(int value, int delta) => Math.Clamp(value + Math.Sign(delta) * 2, 0, 100);
}

public static class AirplaneRules
{
    /// <summary>
    /// The Airplane mode switch is on when Windows' own flag is, or when Gili's switch turned both radios off: the
    /// switch drives the radios through the public radio API, which leaves Windows' flag alone (plan 3b).
    /// </summary>
    public static bool IsOn(bool systemFlag, bool? wifiOn, bool? bluetoothOn) =>
        systemFlag || (wifiOn == false && bluetoothOn == false);
}
