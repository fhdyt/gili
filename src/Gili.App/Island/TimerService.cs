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
    private TimerMode _mode = TimerMode.Timer;

    public TimerService(TimerStore? store)
    {
        _store = store;
        _timer = store?.LoadTimer() ?? CountdownTimer.Default;
        Pomodoro = store?.LoadPomodoro(PomodoroPlan.Default) ?? PomodoroState.Default;
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

    public bool AnythingRunning => Timer.IsRunning || Pomodoro.Clock.IsRunning || Stopwatch.IsRunning;

    public event Action? Changed;

    /// <summary>"timer", "pomo-focus" or "pomo-break": the card to show (F-ISL-08).</summary>
    public event Action<string>? Finished;

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
            default: Pomodoro = PomodoroState.Reset(PomodoroPlan.Default); break;
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
    public void StartDemoTimer(int seconds)
    {
        _demoTimer = CountdownTimer.Default.WithTotal(seconds).Start(Now);
        Save();
    }

    public void EndDemo()
    {
        if (_demoTimer is null) return;
        _demoTimer = null;
        Save();
    }

    public void StartBreak() => SetPomodoro(Pomodoro.NextPhase(Now, startNow: true, PomodoroPlan.Default));

    public void SkipBreak() => SetPomodoro(Pomodoro.Skip(Now, PomodoroPlan.Default));

    public void StopPomodoro() => SetPomodoro(PomodoroState.Default);

    public void StartFocus() => SetPomodoro(Pomodoro.NextPhase(Now, startNow: true, PomodoroPlan.Default));

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
        var ends = new[] { Timer.EndsAt, Pomodoro.Clock.EndsAt }.OfType<DateTimeOffset>().ToList();
        if (ends.Count == 0) return;
        var wait = ends.Min() - Now;
        _deadline.Interval = wait > TimeSpan.FromMilliseconds(10) ? wait : TimeSpan.FromMilliseconds(10);
        _deadline.Start();
    }
}
