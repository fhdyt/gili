using System.Windows.Threading;
using Gili.Core.Timers;
using Gili.Data;

namespace Gili.App.Island;

public enum TimerMode { Timer, Stopwatch, Pomodoro }

/// <summary>
/// The island's timer, stopwatch and pomodoro (F-TOOL-01..03). Every change is saved, so they survive restarts; one
/// timer waits for the nearest end, and a 1 s tick runs only while something runs (PRD 8.1). Raised on the UI thread.
/// </summary>
public sealed class TimerService
{
    private readonly TimerStore? _store;
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _deadline = new();
    private readonly Func<PomodoroPlan> _plan;
    private readonly List<LabeledTimer> _labeled;
    private TimerMode _mode = TimerMode.Timer;

    public TimerService(TimerStore? store, Func<PomodoroPlan> plan)
    {
        _store = store;
        _plan = plan;
        _timer = store?.LoadTimer() ?? CountdownTimer.Default;
        _labeled = [.. store?.LoadLabeled() ?? []];
        Pomodoro = (store?.LoadPomodoro(plan()) ?? PomodoroState.Initial(plan())).WithPlan(plan());
        Stopwatch = store?.LoadStopwatch() ?? StopwatchState.Default;
        _tick.Tick += (_, _) => Changed?.Invoke();
        _deadline.Tick += (_, _) => Reschedule();
    }

    private CountdownTimer _timer;
    private CountdownTimer? _demoTimer;

    /// <summary>The island timer, or the demo's while the demo plays; only the real one is ever saved.</summary>
    public CountdownTimer Timer
    {
        get => _demoTimer ?? _timer;
        private set
        {
            if (_demoTimer is not null) _demoTimer = value;
            else _timer = value;
        }
    }

    public PomodoroState Pomodoro { get; private set; }

    /// <summary>The pomodoro lengths now in Settings; every phase change and every text uses these.</summary>
    public PomodoroPlan Plan => _plan();

    /// <summary>The labeled timers of Tools, oldest first (F-TOOL-01).</summary>
    public IReadOnlyList<LabeledTimer> Labeled => _labeled;

    public bool CanAddTimer => _labeled.Count < LabeledTimer.MaxCount;

    /// <summary>The label of the demo's timer while the demo plays; null otherwise.</summary>
    public string? DemoLabel { get; private set; }

    /// <summary>The labeled timer that ends first, unless the island timer ends sooner (a tie goes to the island timer).</summary>
    public LabeledTimer? SoonestLabeled
    {
        get
        {
            var soonest = TimerSchedule.Soonest(_labeled.Select(t => t.Clock), Now);
            if (soonest is null || (Timer.IsRunning && Timer.EndsAt <= soonest.EndsAt)) return null;
            return _labeled.First(t => ReferenceEquals(t.Clock, soonest));
        }
    }

    /// <summary>The running timer, island or labeled, that ends first: what the slot shows.</summary>
    public CountdownTimer? SoonestTimer => TimerSchedule.Soonest([Timer, .. _labeled.Select(t => t.Clock)], Now);

    public StopwatchState Stopwatch { get; private set; }

    /// <summary>The Timer page's segment; remembered while Gili runs.</summary>
    public TimerMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value) return;
            _mode = value;
            Changed?.Invoke();
        }
    }

    public bool AnythingRunning => Timer.IsRunning || Pomodoro.Clock.IsRunning || Stopwatch.IsRunning || _labeled.Any(t => t.Clock.IsRunning);

    public event Action? Changed;

    /// <summary>"timer", "timer:&lt;id&gt;", "pomo-focus" or "pomo-break": the card to show (F-ISL-08).</summary>
    public event Action<string>? Finished;

    /// <summary>A labeled timer's duration changed or it was deleted: the card it raised no longer applies.</summary>
    public event Action<string>? Stale;

    /// <summary>A labeled timer got a new label: an open card of that timer shows it (the card id).</summary>
    public event Action<string>? Relabeled;

    private static DateTimeOffset Now => DateTimeOffset.UtcNow;

    /// <summary>The page's main button: start, pause or resume whatever the segment shows.</summary>
    public void StartOrPause()
    {
        switch (Mode)
        {
            case TimerMode.Timer:
                Timer = Timer.IsRunning ? Timer.Pause(Now) : Timer.Start(Now);
                break;
            case TimerMode.Stopwatch:
                Stopwatch = Stopwatch.IsRunning ? Stopwatch.Pause(Now) : Stopwatch.Start(Now);
                break;
            default:
                Pomodoro = Pomodoro.Clock.IsRunning ? Pomodoro.Pause(Now) : Pomodoro.Start(Now);
                break;
        }
        Save();
    }

    public void Reset()
    {
        switch (Mode)
        {
            case TimerMode.Timer: Timer = Timer.Reset(); break;
            case TimerMode.Stopwatch: Stopwatch = Stopwatch.Reset(); break;
            default: Pomodoro = PomodoroState.Reset(_plan()); break;
        }
        Save();
    }

    public void Preset(int seconds)
    {
        Timer = Timer.WithTotal(seconds);
        Mode = TimerMode.Timer;
        Save();
    }

    public void Repeat()
    {
        Timer = Timer.Reset().Start(Now);
        Save();
    }

    /// <summary>A timer for the demo only (F-BAR-09): it runs and ends like a real one but is never saved.</summary>
    public void StartDemoTimer(int seconds, string? label = null)
    {
        DemoLabel = label;
        _demoTimer = CountdownTimer.Default.WithTotal(seconds).Start(Now);
        Save();
    }

    public void EndDemo()
    {
        if (_demoTimer is null) return;
        _demoTimer = null;
        DemoLabel = null;
        Save();
    }

    public void StartBreak() => SetPomodoro(Pomodoro.NextPhase(Now, startNow: true, _plan()));

    public void SkipBreak() => SetPomodoro(Pomodoro.Skip(Now, _plan()));

    public void StopPomodoro() => SetPomodoro(PomodoroState.Initial(_plan()));

    public void StartFocus() => SetPomodoro(Pomodoro.NextPhase(Now, startNow: true, _plan()));

    /// <summary>Adds a labeled timer, ready to start; null at the limit. An empty label reads Timer.</summary>
    public LabeledTimer? AddTimer(string label, int seconds)
    {
        if (!CanAddTimer) return null;
        var clock = CountdownTimer.Default.WithTotal(seconds);
        var timer = _store?.AddLabeled(label, clock) ?? new LabeledTimer(
            _labeled.Count == 0 ? 1 : _labeled.Max(t => t.Id) + 1, LabeledTimer.CleanLabel(label), clock);
        _labeled.Add(timer);
        Arm();
        Changed?.Invoke();
        return timer;
    }

    /// <summary>A new label keeps the timer going; a new duration takes it back to ready and closes its card (plan 2g decision 4).</summary>
    public void EditTimer(long id, string label, int seconds)
    {
        if (Find(id) is not { } timer) return;
        var clock = seconds == timer.Clock.TotalSeconds ? timer.Clock : timer.Clock.WithTotal(seconds);
        var cleaned = LabeledTimer.CleanLabel(label);
        Replace(timer with { Label = cleaned, Clock = clock });
        if (!ReferenceEquals(clock, timer.Clock)) Stale?.Invoke(CardId(id));
        else if (cleaned != timer.Label) Relabeled?.Invoke(CardId(id));
    }

    public void DeleteTimer(long id)
    {
        if (Find(id) is null) return;
        _store?.DeleteLabeled(id);
        _labeled.RemoveAll(t => t.Id == id);
        Arm();
        Changed?.Invoke();
        Stale?.Invoke(CardId(id));
    }

    public void StartOrPause(long id)
    {
        if (Find(id) is not { } timer) return;
        var starting = !timer.Clock.IsRunning;
        Replace(timer with { Clock = starting ? timer.Clock.Start(Now) : timer.Clock.Pause(Now) });
        // Started from Tools while its card shows: the card is answered.
        if (starting) Stale?.Invoke(CardId(id));
    }

    public void ResetTimer(long id)
    {
        if (Find(id) is not { } timer) return;
        Replace(timer with { Clock = timer.Clock.Reset() });
        Stale?.Invoke(CardId(id));
    }

    public void RepeatTimer(long id)
    {
        if (Find(id) is { } timer) Replace(timer with { Clock = timer.Clock.Reset().Start(Now) });
    }

    public void Lap()
    {
        if (!Stopwatch.CanLap) return;
        Stopwatch = Stopwatch.Lap(Now);
        Save();
    }

    public void StartOrPauseStopwatch()
    {
        Stopwatch = Stopwatch.IsRunning ? Stopwatch.Pause(Now) : Stopwatch.Start(Now);
        Save();
    }

    public void ResetStopwatch()
    {
        Stopwatch = Stopwatch.Reset();
        Save();
    }

    public void StartOrPausePomodoro()
    {
        Pomodoro = Pomodoro.Clock.IsRunning ? Pomodoro.Pause(Now) : Pomodoro.Start(Now);
        Save();
    }

    public void ResetPomodoro()
    {
        Pomodoro = PomodoroState.Reset(_plan());
        Save();
    }

    /// <summary>Settings changed: the pomodoro takes the new lengths where it may (a phase in progress keeps its own).</summary>
    public void ApplyPlan()
    {
        var next = Pomodoro.WithPlan(_plan());
        if (next == Pomodoro) return;
        Pomodoro = next;
        Save();
    }

    public static string CardId(long id) => $"timer:{id}";

    private LabeledTimer? Find(long id) => _labeled.FirstOrDefault(t => t.Id == id);

    private void Replace(LabeledTimer timer)
    {
        _labeled[_labeled.FindIndex(t => t.Id == timer.Id)] = timer;
        _store?.UpdateLabeled(timer);
        Arm();
        Changed?.Invoke();
    }

    /// <summary>
    /// Ends whatever has reached its end, raising its card, then waits for the next end. Called at start (a timer may
    /// have ended while Gili was closed, plan 2b decision 4), on waking, and when the clock changes.
    /// </summary>
    public void Reschedule()
    {
        var now = Now;
        var finished = new List<string>();
        if (Timer.IsDone(now))
        {
            Timer = Timer.Reset();
            finished.Add("timer");
        }
        foreach (var id in TimerSchedule.Finished(_labeled, now))
        {
            var timer = Find(id)!;
            Replace(timer with { Clock = timer.Clock.Reset() });
            finished.Add(CardId(id));
        }
        if (Pomodoro.Clock.IsDone(now))
        {
            finished.Add(Pomodoro.Phase == PomodoroPhase.Focus ? "pomo-focus" : "pomo-break");
            Pomodoro = Pomodoro with { Clock = Pomodoro.Clock.Reset() };
        }
        if (finished.Count > 0) Save();
        else Arm();
        foreach (var card in finished) Finished?.Invoke(card);
    }

    private void SetPomodoro(PomodoroState pomodoro)
    {
        Pomodoro = pomodoro;
        Save();
    }

    private void Save()
    {
        _store?.SaveTimer(_timer);
        _store?.SavePomodoro(Pomodoro);
        _store?.SaveStopwatch(Stopwatch);
        Arm();
        Changed?.Invoke();
    }

    /// <summary>The 1 s tick while anything runs, and one timer to the nearest end.</summary>
    private void Arm()
    {
        if (AnythingRunning) _tick.Start();
        else _tick.Stop();
        _deadline.Stop();
        // The nearest end, even one already past (it then fires at once).
        var ends = new[] { Timer.EndsAt, Pomodoro.Clock.EndsAt }.Concat(_labeled.Select(t => t.Clock.EndsAt)).OfType<DateTimeOffset>().ToList();
        if (ends.Count == 0) return;
        var wait = ends.Min() - Now;
        _deadline.Interval = wait > TimeSpan.FromMilliseconds(10) ? wait : TimeSpan.FromMilliseconds(10);
        _deadline.Start();
    }
}
