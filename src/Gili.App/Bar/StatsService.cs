using System.Windows.Threading;
using Gili.Core.Modules;

namespace Gili.App.Bar;

public sealed record StatsSnapshot(
    ModuleState<CpuReading> Cpu,
    ModuleState<MemoryReading> Memory,
    ModuleState<NetworkReading> Network);

/// <summary>Samples CPU, RAM and network once a second off the UI thread (F-RIGHT-01).</summary>
public sealed class StatsService : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly ModuleSampler<CpuReading> _cpu;
    private readonly ModuleSampler<MemoryReading> _memory;
    private readonly ModuleSampler<NetworkReading> _network;
    private readonly CancellationTokenSource _stop = new();
    private volatile bool _paused;

    public StatsService(Dispatcher dispatcher, Func<CpuReading> cpu, Func<MemoryReading> memory, Func<NetworkReading> network)
    {
        _dispatcher = dispatcher;
        _cpu = new ModuleSampler<CpuReading>(cpu);
        _memory = new ModuleSampler<MemoryReading>(memory);
        _network = new ModuleSampler<NetworkReading>(network);
    }

    /// <summary>Set while the bar is hidden by a fullscreen app or the laptop sleeps (PRD 8.1).</summary>
    public bool Paused
    {
        get => _paused;
        set => _paused = value;
    }

    /// <summary>Raised on the UI thread.</summary>
    public event Action<StatsSnapshot>? Updated;

    public void Start() => _ = Task.Run(RunAsync);

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }

    private async Task RunAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            do
            {
                if (_paused) continue;
                var now = DateTimeOffset.UtcNow;
                var snapshot = new StatsSnapshot(_cpu.Tick(now), _memory.Tick(now), _network.Tick(now));
                _ = _dispatcher.InvokeAsync(() => Updated?.Invoke(snapshot));
            }
            while (await timer.WaitForNextTickAsync(_stop.Token));
        }
        catch (OperationCanceledException)
        {
            // App is closing.
        }
    }
}
