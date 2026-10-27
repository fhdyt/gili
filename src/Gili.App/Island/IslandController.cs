using System.Windows.Threading;
using Gili.Core.Island;
using Gili.Core.Power;
using Gili.Platform.Power;

namespace Gili.App.Island;

/// <summary>Feeds the providers into the IslandCoordinator and runs its timers for wide notices and reminder cards.</summary>
public sealed class IslandController : IDisposable
{
    private readonly IslandCoordinator _coordinator = new(() => DateTimeOffset.Now);
    private readonly DispatcherTimer _deadline = new(DispatcherPriority.Normal);
    private readonly PowerMonitor _power = new();

    public IslandController(Dispatcher dispatcher, MediaService media)
    {
        Media = media;
        _deadline.Tick += (_, _) =>
        {
            _deadline.Stop();
            _coordinator.Tick();
            ScheduleDeadline();
        };
        _coordinator.Changed += view =>
        {
            ScheduleDeadline();
            ViewChanged?.Invoke(view);
        };
        media.Updated += () => _coordinator.SetActivity(ActivityKind.Media, media.IsActive);
        _power.Changed += snapshot => dispatcher.InvokeAsync(() => ApplyPower(snapshot));
        ApplyPower(PowerMonitor.Read());
    }

    public IslandView View => _coordinator.View;

    public MediaService Media { get; }

    public PowerSnapshot? Power { get; private set; }

    public event Action<IslandView>? ViewChanged;

    public event Action? PowerChanged;

    public void OpenFromIsland() => _coordinator.OpenFor(View.Slot);

    public void OpenBubble() => _coordinator.OpenFor(View.Bubble);

    public void Open(IslandPage page) => _coordinator.Open(page);

    public void Toggle(IslandPage page) => _coordinator.Toggle(page);

    public void Close() => _coordinator.Close();

    /// <summary>The pointer went down outside the open island.</summary>
    public void Dismiss() => _coordinator.Dismiss();

    public void SetFullscreen(bool fullscreen) => _coordinator.SetFullscreen(fullscreen);

    public void Dispose()
    {
        _deadline.Stop();
        _power.Dispose();
    }

    private void ApplyPower(PowerSnapshot snapshot)
    {
        var wide = PowerRules.WideFor(Power, snapshot);
        Power = snapshot;
        PowerChanged?.Invoke();
        _coordinator.SetActivity(ActivityKind.Charging, PowerRules.IsCharging(snapshot));
        if (wide is not null) _coordinator.ShowWide(wide);
    }

    private void ScheduleDeadline()
    {
        _deadline.Stop();
        if (_coordinator.NextDeadline is not { } at) return;
        var wait = at - DateTimeOffset.Now;
        _deadline.Interval = wait > TimeSpan.FromMilliseconds(10) ? wait : TimeSpan.FromMilliseconds(10);
        _deadline.Start();
    }
}
