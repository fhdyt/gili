namespace Gili.Core.Settings;

/// <summary>The right side's modules that Settings → Right side can hide (PRD 6.4), stored as names in settings.json.</summary>
[Flags]
public enum RightModules
{
    None = 0,
    Weather = 1,
    Network = 2,
    Cpu = 4,
    Ram = 8,
    Gpu = 16,
    Disk = 32,
    Wifi = 64,
    Bluetooth = 128,
    Volume = 256,
    Brightness = 512,
    Tray = 1024,
    Clock = 2048,
}

/// <summary>
/// The bar's module ids and the order of the six system modules, which Settings can rearrange (PRD 6.4); Wi‑Fi through
/// Power keep their place.
/// </summary>
public static class RightModuleRules
{
    public static IReadOnlyList<string> DefaultOrder { get; } = ["weather", "net", "cpu", "ram", "gpu", "disk"];

    public static RightModules Of(string id) => id switch
    {
        "weather" => RightModules.Weather,
        "net" => RightModules.Network,
        "cpu" => RightModules.Cpu,
        "ram" => RightModules.Ram,
        "gpu" => RightModules.Gpu,
        "disk" => RightModules.Disk,
        "wifi" => RightModules.Wifi,
        "bt" => RightModules.Bluetooth,
        "vol" => RightModules.Volume,
        "bri" => RightModules.Brightness,
        "tray" => RightModules.Tray,
        "clock" => RightModules.Clock,
        _ => RightModules.None,
    };

    /// <summary>Keeps the known ids once each, in their order, and adds any missing one at the end.</summary>
    public static IReadOnlyList<string> Repair(IEnumerable<string>? order)
    {
        var known = (order ?? []).Select(id => id.Trim()).Where(DefaultOrder.Contains).Distinct().ToList();
        known.AddRange(DefaultOrder.Except(known));
        return known;
    }

    /// <summary>One step up (−1) or down (+1); at either end, or for a module that has no place in the order, nothing moves.</summary>
    public static IReadOnlyList<string> Move(IReadOnlyList<string> order, string id, int delta)
    {
        var from = IndexOf(order, id);
        return from < 0 ? order : MoveTo(order, id, from + delta);
    }

    /// <summary>Puts the module at <paramref name="index"/>, clamped to the list.</summary>
    public static IReadOnlyList<string> MoveTo(IReadOnlyList<string> order, string id, int index)
    {
        if (IndexOf(order, id) < 0) return order;
        var list = order.Where(x => x != id).ToList();
        list.Insert(Math.Clamp(index, 0, list.Count), id);
        return list;
    }

    private static int IndexOf(IReadOnlyList<string> order, string id)
    {
        for (var i = 0; i < order.Count; i++)
        {
            if (order[i] == id) return i;
        }
        return -1;
    }
}
