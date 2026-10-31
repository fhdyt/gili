using System.Windows.Threading;
using Gili.Core.Modules;

namespace Gili.App.Bar;

/// <summary>The last minute of each charted reading on the System page, oldest first.</summary>
public sealed record StatsHistory(IReadOnlyList<double> Cpu, IReadOnlyList<double> Ram, IReadOnlyList<double> Gpu, IReadOnlyList<double> Down);

public sealed record StatsSnapshot(
    ModuleState<CpuReading> Cpu,
    ModuleState<MemoryReading> Memory,
    ModuleState<NetworkReading> Network,
    ModuleState<GpuReading> Gpu,
    ModuleState<DiskReading> Disk,
    StatsHistory History);

/// <summary>
/// Samples CPU, RAM, network and GPU once a second and disk every 10 s, off the UI thread (F-RIGHT-01, F-RIGHT-03), and
/// only what <see cref="Demand"/> asks for: a reading nobody shows is not read at all (PRD 8.1).
/// </summary>
public sealed class StatsService : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Metric<CpuReading> _cpu;
    private readonly Metric<MemoryReading> _memory;
    private readonly Metric<NetworkReading> _network;
    private readonly Metric<GpuReading> _gpu;
    private readonly Metric<DiskReading> _disk;
    private readonly CancellationTokenSource _stop = new();
    private volatile bool _paused;
    private volatile int _demand = (int)StatsDemand.All;

    /// <summary>Each factory makes a fresh sampler, since a reading that stops and starts again begins anew.</summary>
    public StatsService(Dispatcher dispatcher, Func<Func<CpuReading>> cpu, Func<Func<MemoryReading>> memory, Func<Func<NetworkReading>> network,
        Func<Func<GpuReading>> gpu, Func<Func<DiskReading>> disk)
    {
        _dispatcher = dispatcher;
        // CPU and GPU counters read 0 the first time, so that reading is dropped.
        _cpu = new Metric<CpuReading>(cpu, StatsDemand.Cpu, warmUp: true, every: 1, c => c.Percent);
        _memory = new Metric<MemoryReading>(memory, StatsDemand.Memory, warmUp: false, every: 1, m => m.Percent);
        _network = new Metric<NetworkReading>(network, StatsDemand.Network, warmUp: false, every: 1, n => n.DownBytesPerSecond);
        _gpu = new Metric<GpuReading>(gpu, StatsDemand.Gpu, warmUp: true, every: 1, g => g.Percent);
        _disk = new Metric<DiskReading>(disk, StatsDemand.Disk, warmUp: false, every: 10, null);
    }

    /// <summary>Set while the bar is hidden by a fullscreen app or the laptop sleeps (PRD 8.1).</summary>
    public bool Paused
    {
        get => _paused;
        set => _paused = value;
    }

    /// <summary>What the bar and the System page show; the rest stops being read from the next second.</summary>
    public StatsDemand Demand
    {
        get => (StatsDemand)_demand;
        set => _demand = (int)value;
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
                var demand = Demand;
                var snapshot = new StatsSnapshot(
                    _cpu.Tick(now, demand), _memory.Tick(now, demand), _network.Tick(now, demand), _gpu.Tick(now, demand), _disk.Tick(now, demand),
                    new StatsHistory(_cpu.History.Values, _memory.History.Values, _gpu.History.Values, _network.History.Values));
                _ = _dispatcher.InvokeAsync(() => Updated?.Invoke(snapshot));
            }
            while (await timer.WaitForNextTickAsync(_stop.Token));
        }
        catch (OperationCanceledException)
        {
            // App is closing.
        }
        finally
        {
            foreach (var metric in new IMetric[] { _cpu, _memory, _network, _gpu, _disk }) metric.Stop();
        }
    }

    private interface IMetric
    {
        void Stop();
    }

    /// <summary>One reading: its sampler lives only while asked for, and its minute of history with it.</summary>
    private sealed class Metric<T>(Func<Func<T>> create, StatsDemand flag, bool warmUp, int every, Func<T, double>? chart) : IMetric
        where T : class
    {
        private ModuleSampler<T>? _sampler;
        private IDisposable? _owner;
        private ModuleState<T> _state;
        private int _ticks;

        public MetricHistory History { get; } = new();

        public ModuleState<T> Tick(DateTimeOffset now, StatsDemand demand)
        {
            if (!demand.HasFlag(flag))
            {
                Stop();
                return default;
            }
            if (_sampler is null)
            {
                var sample = create();
                // A method group's target is the sampler object; counters behind it are released when it stops.
                _owner = sample.Target as IDisposable;
                _sampler = new ModuleSampler<T>(sample);
                _ticks = 0;
                if (warmUp)
                {
                    _sampler.Tick(now);
                    return default;
                }
            }
            if (_ticks++ % every != 0) return _state;
            _state = _sampler.Tick(now);
            if (chart is not null && _state.Value is { } value) History.Add(chart(value));
            return _state;
        }

        public void Stop()
        {
            if (_sampler is null) return;
            _sampler = null;
            _owner?.Dispose();
            _owner = null;
            _state = default;
            History.Clear();
        }
    }
}
