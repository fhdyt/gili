using Gili.Core.Devices;
using Gili.Core.Island;

namespace Gili.Core.Tests.Devices;

public class BluetoothRulesTests
{
    [Theory]
    [InlineData(0x240404u, BluetoothKind.Audio)]
    [InlineData(0x002540u, BluetoothKind.Keyboard)]
    [InlineData(0x002580u, BluetoothKind.Mouse)]
    [InlineData(0x0025C0u, BluetoothKind.Keyboard)]
    [InlineData(0x002508u, BluetoothKind.Gamepad)]
    [InlineData(0x002504u, BluetoothKind.Gamepad)]
    [InlineData(0x5A020Cu, BluetoothKind.Phone)]
    [InlineData(0x1F00u, BluetoothKind.Other)]
    public void Class_of_device_maps_to_kind(uint cod, BluetoothKind kind) => Assert.Equal(kind, BluetoothRules.KindFromClass(cod));

    [Theory]
    [InlineData(0x03C1, BluetoothKind.Keyboard)]
    [InlineData(0x03C2, BluetoothKind.Mouse)]
    [InlineData(0x03C3, BluetoothKind.Gamepad)]
    [InlineData(0x03C4, BluetoothKind.Gamepad)]
    [InlineData(0x0941, BluetoothKind.Audio)]
    [InlineData(0x0841, BluetoothKind.Audio)]
    [InlineData(0x0040, BluetoothKind.Phone)]
    [InlineData(0, BluetoothKind.Other)]
    public void Appearance_maps_to_kind(int appearance, BluetoothKind kind) =>
        Assert.Equal(kind, BluetoothRules.KindFromAppearance((ushort)appearance));

    [Fact]
    public void Connected_text_has_name_state_and_battery()
    {
        var wide = BluetoothRules.WideFor(new BluetoothChange("dev1", "Galaxy Buds2 Pro", BluetoothKind.Audio, Connected: true, Battery: 80));
        Assert.Equal("bt:dev1", wide.Key);
        Assert.Equal(WideIcon.Headphones, wide.Icon);
        Assert.Equal(WideTone.Blue, wide.Tone);
        Assert.Equal("Galaxy Buds2 Pro Connected 80%", wide.Text);
        Assert.Equal(new WideRun("Connected", WideRunStyle.Muted, SpaceBefore: true), wide.Runs[1]);
        Assert.Equal(new WideRun("80%", WideRunStyle.Number, SpaceBefore: true), wide.Runs[2]);
        Assert.Equal(TimeSpan.FromSeconds(4), wide.Duration);
    }

    [Fact]
    public void Connected_without_battery_has_two_parts()
    {
        var wide = BluetoothRules.WideFor(new BluetoothChange("m", "MX Master 3", BluetoothKind.Mouse, true, null));
        Assert.Equal(WideIcon.Mouse, wide.Icon);
        Assert.Equal("MX Master 3 Connected", wide.Text);
    }

    [Fact]
    public void Disconnected_is_muted_and_short()
    {
        var wide = BluetoothRules.WideFor(new BluetoothChange("dev1", "Galaxy Buds2 Pro", BluetoothKind.Audio, Connected: false, Battery: 80));
        Assert.Equal(WideTone.Muted, wide.Tone);
        Assert.Equal("Galaxy Buds2 Pro Disconnected", wide.Text);
        Assert.Equal(TimeSpan.FromSeconds(2), wide.Duration);
    }

    [Fact]
    public void Other_devices_use_the_bluetooth_icon() =>
        Assert.Equal(WideIcon.Bluetooth, BluetoothRules.WideFor(new BluetoothChange("x", "Car", BluetoothKind.Other, true, null)).Icon);

    [Fact]
    public void Unnamed_device_reads_bluetooth_device() =>
        Assert.Equal("Bluetooth device Connected", BluetoothRules.WideFor(new BluetoothChange("x", "  ", BluetoothKind.Other, true, null)).Text);

    [Fact]
    public void Long_names_are_cut_at_32() =>
        Assert.Equal(new string('a', 31) + "…",
            BluetoothRules.WideFor(new BluetoothChange("x", new string('a', 40), BluetoothKind.Other, false, null)).Runs[0].Text);
}
