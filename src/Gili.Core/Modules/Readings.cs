namespace Gili.Core.Modules;

public sealed record CpuReading(double Percent);

public sealed record GpuReading(double Percent);

/// <summary>Free space on Windows' own drive; <paramref name="Drive"/> is its letter with a colon ("C:").</summary>
public sealed record DiskReading(string Drive, ulong FreeBytes, ulong TotalBytes);

public sealed record MemoryReading(double Percent, ulong UsedBytes, ulong TotalBytes);

/// <summary>
/// <paramref name="Adapters"/> lists the adapter names for the tooltip; <paramref name="UpBytes"/> and
/// <paramref name="DownBytes"/> are what moved since the last sample, for today's total.
/// </summary>
public sealed record NetworkReading(double UpBytesPerSecond, double DownBytesPerSecond, string Adapters, double UpBytes = 0, double DownBytes = 0);
