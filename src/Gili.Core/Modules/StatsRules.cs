using Gili.Core.Settings;

namespace Gili.Core.Modules;

/// <summary>The readings the bar and the System page ask for; the others are not read at all (PRD 8.1).</summary>
[Flags]
public enum StatsDemand { None = 0, Cpu = 1, Memory = 2, Network = 4, Gpu = 8, Disk = 16, All = 31 }

public static class StatsDemands
{
    /// <summary>
    /// A switched-on module asks for its reading, even while it sits in "…" (whose list shows it); the open System page
    /// asks for all five.
    /// </summary>
    public static StatsDemand For(RightModules hidden, bool systemPageOpen)
    {
        if (systemPageOpen) return StatsDemand.All;
        var demand = StatsDemand.None;
        if (!hidden.HasFlag(RightModules.Cpu)) demand |= StatsDemand.Cpu;
        if (!hidden.HasFlag(RightModules.Ram)) demand |= StatsDemand.Memory;
        if (!hidden.HasFlag(RightModules.Network)) demand |= StatsDemand.Network;
        if (!hidden.HasFlag(RightModules.Gpu)) demand |= StatsDemand.Gpu;
        if (!hidden.HasFlag(RightModules.Disk)) demand |= StatsDemand.Disk;
        return demand;
    }
}

/// <summary>
/// GPU use as Task Manager's GPU column counts it: every process's share of an engine
/// (<c>pid_1234_luid_…_phys_0_eng_0_engtype_3D</c>) added up per engine, and the busiest engine wins.
/// </summary>
public static class GpuEngines
{
    public static double Utilization(IEnumerable<(string Instance, double Value)> samples)
    {
        var engines = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (instance, value) in samples)
        {
            var engine = Engine(instance);
            engines[engine] = engines.GetValueOrDefault(engine) + Math.Max(0, value);
        }
        return engines.Count == 0 ? 0 : Math.Min(100, engines.Values.Max());
    }

    /// <summary>The instance without its <c>pid_N_</c> prefix.</summary>
    private static string Engine(string instance)
    {
        if (!instance.StartsWith("pid_", StringComparison.Ordinal)) return instance;
        var end = instance.IndexOf('_', 4);
        return end < 0 ? instance : instance[(end + 1)..];
    }
}

/// <summary>The last minute of one reading, a value a second, for the System page's charts.</summary>
public sealed class MetricHistory(int capacity = 60)
{
    private readonly Queue<double> _values = new(capacity);
    private double[] _snapshot = [];

    public IReadOnlyList<double> Values => _snapshot;

    public void Add(double value)
    {
        if (_values.Count == capacity) _values.Dequeue();
        _values.Enqueue(value);
        _snapshot = [.. _values];
    }

    public void Clear()
    {
        _values.Clear();
        _snapshot = [];
    }
}

/// <summary>The System page's mini charts (mockup <c>sparkPts</c>): the newest value on the right edge, a second per step.</summary>
public static class Sparkline
{
    /// <summary>Download charts scale to the busiest second of the minute, but never below 100 KB/s.</summary>
    public const double DownloadFloor = 100 * 1024;

    public static IReadOnlyList<(double X, double Y)> Points(IReadOnlyList<double> values, double max, double width, double height, int capacity = 60)
    {
        var step = width / (capacity - 1);
        var points = new (double X, double Y)[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            var share = max <= 0 ? 0 : Math.Clamp(values[i] / max, 0, 1);
            points[i] = (width - (values.Count - 1 - i) * step, height - 1 - share * (height - 3));
        }
        return points;
    }

    public static double DownloadMax(IReadOnlyList<double> bytesPerSecond) =>
        bytesPerSecond.Count == 0 ? DownloadFloor : Math.Max(DownloadFloor, bytesPerSecond.Max());
}
