using Gili.Core.Devices;
using Gili.Core.Modules;

namespace Gili.Core.Tests.Modules;

public class ControlRulesTests
{
    private static WifiNetwork Net(string ssid, int quality = 50, bool secured = true, bool profile = false, bool connected = false) =>
        new(ssid, quality, secured, profile, connected, profile ? ssid : null);

    [Fact]
    public void Same_name_merges_and_keeps_connected()
    {
        var rows = WifiNetworks.Order([Net("Home", 90, profile: true), Net("Home", 40, profile: true, connected: true)]);
        var row = Assert.Single(rows);
        Assert.Equal((WifiStatus.Connected, "Home"), (row.Status, row.Profile));
    }

    [Fact]
    public void A_profile_from_either_entry_is_kept()
    {
        var rows = WifiNetworks.Order([Net("Cafe", 90), Net("Cafe", 40, profile: true)]);
        Assert.Equal((WifiStatus.Saved, "Cafe"), (rows[0].Status, rows[0].Profile));
    }

    [Fact]
    public void Hidden_networks_are_dropped() =>
        Assert.Equal(["Home"], WifiNetworks.Order([Net(""), Net("  "), Net("Home")]).Select(r => r.Ssid));

    [Fact]
    public void Statuses()
    {
        var rows = WifiNetworks.Order([
            Net("A", 10, connected: true, profile: true), Net("B", 90, profile: true), Net("C", 80), Net("D", 70, secured: false)]);
        Assert.Equal(["Connected", "Saved", "Secured", "Open"], rows.Select(r => r.StatusText));
    }

    [Fact]
    public void Order_is_connected_then_signal_then_name() =>
        Assert.Equal(
            ["Z", "B", "A", "C"],
            WifiNetworks.Order([Net("A", 50), Net("B", 80), Net("C", 50), Net("Z", 5, connected: true, profile: true)]).Select(r => r.Ssid));

    [Fact]
    public void Unnamed_and_bad_battery()
    {
        var device = BluetoothList.Order([new PairedDevice("1", " ", BluetoothKind.Audio, true, 140, true)])[0];
        Assert.Equal(("Bluetooth device", "Connected"), (device.Name, BluetoothList.Status(device)));
    }

    [Fact]
    public void Bluetooth_status_texts()
    {
        Assert.Equal("Connected · 80%", BluetoothList.Status(new PairedDevice("1", "Buds", BluetoothKind.Audio, true, 80, true)));
        Assert.Equal("Paired", BluetoothList.Status(new PairedDevice("1", "Buds", BluetoothKind.Audio, false, 80, true)));
    }

    [Fact]
    public void Bluetooth_order_connected_first_then_name() =>
        Assert.Equal(
            ["Mouse", "buds", "Phone"],
            BluetoothList.Order([
                new PairedDevice("1", "Phone", BluetoothKind.Phone, false, null, false),
                new PairedDevice("2", "buds", BluetoothKind.Audio, false, null, true),
                new PairedDevice("3", "Mouse", BluetoothKind.Mouse, true, null, false)]).Select(d => d.Name));

    [Theory]
    [InlineData(99, 1, 100)]
    [InlineData(1, -1, 0)]
    [InlineData(50, 120, 52)]
    [InlineData(50, -120, 48)]
    [InlineData(50, 0, 50)]
    public void Wheel_clamps(int value, int delta, int expected) => Assert.Equal(expected, ControlSliders.Wheel(value, delta));

    [Fact]
    public void Airplane_follows_system_flag() => Assert.True(AirplaneRules.IsOn(systemFlag: true, wifiOn: true, bluetoothOn: true));

    [Fact]
    public void Both_radios_off_reads_as_airplane() => Assert.True(AirplaneRules.IsOn(false, false, false));

    [Fact]
    public void One_radio_missing_is_not_airplane()
    {
        Assert.False(AirplaneRules.IsOn(false, false, null));
        Assert.False(AirplaneRules.IsOn(false, false, true));
    }
}
