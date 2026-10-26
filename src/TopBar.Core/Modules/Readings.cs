namespace TopBar.Core.Modules;

public sealed record CpuReading(double Percent);

public sealed record MemoryReading(double Percent, ulong UsedBytes, ulong TotalBytes);

/// <summary><paramref name="Adapters"/> lists the adapter names for the tooltip.</summary>
public sealed record NetworkReading(double UpBytesPerSecond, double DownBytesPerSecond, string Adapters);
