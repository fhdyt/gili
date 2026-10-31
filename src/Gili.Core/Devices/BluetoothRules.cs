using System.Globalization;
using Gili.Core.Island;

namespace Gili.Core.Devices;

public enum BluetoothKind { Other, Audio, Mouse, Keyboard, Gamepad, Phone }

/// <summary>A Bluetooth device that connected or disconnected after Gili started; <paramref name="Battery"/> in percent when reported.</summary>
public sealed record BluetoothChange(string Id, string Name, BluetoothKind Kind, bool Connected, int? Battery);

/// <summary>The device kind for its icon, and the wide notice for a connect or disconnect (PRD 6.3, F-ISL-18).</summary>
public static class BluetoothRules
{
    private const int NameLimit = 32;

    /// <summary>A classic device's Class of Device: major class in bits 8–12, the peripheral's minor class below it.</summary>
    public static BluetoothKind KindFromClass(uint classOfDevice)
    {
        var major = (classOfDevice >> 8) & 0x1F;
        if (major == 2) return BluetoothKind.Phone;
        if (major == 4) return BluetoothKind.Audio;
        if (major != 5) return BluetoothKind.Other;
        var pointerOrKeys = (classOfDevice >> 6) & 0x3;
        if (pointerOrKeys == 2) return BluetoothKind.Mouse;
        if (pointerOrKeys != 0) return BluetoothKind.Keyboard;
        return ((classOfDevice >> 2) & 0xF) is 1 or 2 ? BluetoothKind.Gamepad : BluetoothKind.Other;
    }

    /// <summary>An LE device's GAP appearance: category in the top 10 bits, subcategory in the low 6.</summary>
    public static BluetoothKind KindFromAppearance(ushort appearance) => (appearance >> 6) switch
    {
        1 => BluetoothKind.Phone,
        15 => (appearance & 0x3F) switch
        {
            1 => BluetoothKind.Keyboard,
            2 => BluetoothKind.Mouse,
            3 or 4 => BluetoothKind.Gamepad,
            _ => BluetoothKind.Other,
        },
        33 or 37 => BluetoothKind.Audio,
        _ => BluetoothKind.Other,
    };

    /// <summary>Keyed per device, so a device that flaps replaces its own notice instead of queueing more.</summary>
    public static WideEvent WideFor(BluetoothChange change)
    {
        var name = new WideRun(Name(change.Name));
        if (!change.Connected)
            return new("bt:" + change.Id, Icon(change.Kind), WideTone.Muted,
                [name, new WideRun("Disconnected", WideRunStyle.Muted, SpaceBefore: true)], TimeSpan.FromSeconds(2));
        List<WideRun> runs = [name, new WideRun("Connected", WideRunStyle.Muted, SpaceBefore: true)];
        if (change.Battery is { } battery)
            runs.Add(new WideRun(battery.ToString(CultureInfo.InvariantCulture) + "%", WideRunStyle.Number, SpaceBefore: true));
        return new("bt:" + change.Id, Icon(change.Kind), WideTone.Blue, runs, TimeSpan.FromSeconds(4));
    }

    private static string Name(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0) return "Bluetooth device";
        return trimmed.Length > NameLimit ? trimmed[..(NameLimit - 1)] + "…" : trimmed;
    }

    private static WideIcon Icon(BluetoothKind kind) => kind switch
    {
        BluetoothKind.Audio => WideIcon.Headphones,
        BluetoothKind.Mouse => WideIcon.Mouse,
        BluetoothKind.Keyboard => WideIcon.Keyboard,
        BluetoothKind.Gamepad => WideIcon.Gamepad,
        BluetoothKind.Phone => WideIcon.Phone,
        _ => WideIcon.Bluetooth,
    };
}
