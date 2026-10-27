using System.Text.RegularExpressions;

namespace Gili.Core.Modules;

/// <summary>
/// Which adapters the network module adds up. Windows also lists every filter driver stacked on an adapter
/// (<c>Wi-Fi-QoS Packet Scheduler-0000</c>) with that adapter's own counters, so counting them multiplied the speed;
/// WAN miniports and the Hyper-V switch carry no traffic of their own.
/// </summary>
public static partial class NetworkAdapters
{
    public static IReadOnlyList<(string Name, string Description)> Counted(IEnumerable<(string Name, string Description)> adapters)
    {
        var all = adapters.ToList();
        return [.. all.Where(a =>
            !FilterLayer().IsMatch(a.Name) &&
            !all.Any(b => b.Name != a.Name && a.Name.StartsWith(b.Name + "-", StringComparison.Ordinal)) &&
            !a.Description.StartsWith("WAN Miniport", StringComparison.OrdinalIgnoreCase) &&
            !a.Description.StartsWith("Hyper-V Virtual Switch Extension Adapter", StringComparison.OrdinalIgnoreCase))];
    }

    [GeneratedRegex(@"-\d{4}$")]
    private static partial Regex FilterLayer();
}
