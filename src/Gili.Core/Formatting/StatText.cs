using System.Globalization;

namespace Gili.Core.Formatting;

/// <summary>Numbers in the right-hand modules (PRD 6.4). Always invariant, never the Windows locale.</summary>
public static class StatText
{
    private const double BytesPerGB = 1024 * 1024 * 1024;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string Percent(double value) =>
        Math.Round(Math.Clamp(value, 0, 100), MidpointRounding.AwayFromZero).ToString("0", Invariant) + "%";

    public static string Rate(double bytesPerSecond)
    {
        if (bytesPerSecond < 1024) return Math.Floor(Math.Max(0, bytesPerSecond)).ToString("0", Invariant) + " B/s";
        var kb = Math.Round(bytesPerSecond / 1024, MidpointRounding.AwayFromZero);
        if (kb < 1000) return kb.ToString("0", Invariant) + " KB/s";
        return (bytesPerSecond / (1024 * 1024)).ToString("0.0", Invariant) + " MB/s";
    }

    /// <summary>Free disk space in Explorer's GB (1024³): whole numbers from 10 GB, one decimal below.</summary>
    public static string Disk(ulong bytes)
    {
        var gb = bytes / BytesPerGB;
        return gb >= 10 ? Math.Round(gb, MidpointRounding.AwayFromZero).ToString("0", Invariant) + " GB" : gb.ToString("0.0", Invariant) + " GB";
    }

    public static string DiskFree(ulong bytes) => Disk(bytes) + " free";

    public static string MemoryUsage(ulong usedBytes, ulong totalBytes) =>
        $"{(usedBytes / BytesPerGB).ToString("0.0", Invariant)} / {(totalBytes / BytesPerGB).ToString("0.#", Invariant)} GB";
}
