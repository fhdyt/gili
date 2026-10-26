using System.Diagnostics;
using TopBar.Core.Modules;

namespace TopBar.Platform.Stats;

/// <summary>
/// "% Processor Utility" is the counter Task Manager shows (F-RIGHT-01 allows 5 points of difference);
/// systems without it fall back to "% Processor Time". The first sample after creation reads 0.
/// </summary>
public sealed class CpuSampler : IDisposable
{
    private PerformanceCounter? _counter;

    public CpuReading Sample()
    {
        try
        {
            _counter ??= Create();
            return new CpuReading(Math.Clamp(_counter.NextValue(), 0, 100));
        }
        catch
        {
            // Recreate the counter on the next try instead of reusing a broken one.
            _counter?.Dispose();
            _counter = null;
            throw;
        }
    }

    public void Dispose() => _counter?.Dispose();

    private static PerformanceCounter Create() =>
        PerformanceCounterCategory.CounterExists("% Processor Utility", "Processor Information")
            ? new PerformanceCounter("Processor Information", "% Processor Utility", "_Total", readOnly: true)
            : new PerformanceCounter("Processor", "% Processor Time", "_Total", readOnly: true);
}
