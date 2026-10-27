using System.Globalization;

namespace Gili.Core.Modules;

/// <summary>
/// Bytes sent and received today, for the network module's tooltip (PRD 6.4). Counted in memory from the samples, so
/// after a restart it counts from zero again until Phase 2 has a database (plan 1i decision 3).
/// </summary>
public sealed class NetworkToday
{
    private DateTime _day;

    public (double Up, double Down) Total { get; private set; }

    public void Add(double upBytes, double downBytes, DateTime localNow)
    {
        if (localNow.Date != _day)
        {
            _day = localNow.Date;
            Total = (0, 0);
        }
        Total = (Total.Up + Math.Max(0, upBytes), Total.Down + Math.Max(0, downBytes));
    }

    /// <summary>The adapters, then <c>Today · ↑ 1.2 MB  ↓ 45.0 MB</c>.</summary>
    public string Tooltip(string adapters) => $"{adapters}\nToday · ↑ {Amount(Total.Up)}  ↓ {Amount(Total.Down)}";

    /// <summary>The module's units without "/s", plus GB for a day's total.</summary>
    public static string Amount(double bytes)
    {
        var invariant = CultureInfo.InvariantCulture;
        if (bytes < 1024) return Math.Floor(Math.Max(0, bytes)).ToString("0", invariant) + " B";
        var kb = Math.Round(bytes / 1024, MidpointRounding.AwayFromZero);
        if (kb < 1000) return kb.ToString("0", invariant) + " KB";
        var mb = bytes / (1024 * 1024);
        if (mb < 1000) return mb.ToString("0.0", invariant) + " MB";
        return (bytes / (1024 * 1024 * 1024)).ToString("0.0", invariant) + " GB";
    }
}
