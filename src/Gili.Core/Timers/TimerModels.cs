namespace Gili.Core.Timers;

/// <summary>
/// A countdown kept as an absolute end in UTC while it runs, or the time left while paused, so it stays right across
/// restarts, sleep and time zone changes (PRD 9.3, F-TOOL-01). Every change returns a new value.
/// </summary>
public sealed record CountdownTimer(int TotalSeconds, DateTimeOffset? EndsAt, TimeSpan? PausedRemaining)
{
    /// <summary>The island timer starts at 10:00, as in the mockup.</summary>
    public static readonly CountdownTimer Default = new(600, null, null);

    public bool IsRunning => EndsAt is not null;

    public bool IsPaused => PausedRemaining is not null;

    public TimeSpan Total => TimeSpan.FromSeconds(TotalSeconds);

    public TimeSpan Remaining(DateTimeOffset now) =>
        EndsAt is { } end ? (end > now ? end - now : TimeSpan.Zero) : PausedRemaining ?? Total;

    public bool IsDone(DateTimeOffset now) => EndsAt is { } end && end <= now;

    public CountdownTimer Start(DateTimeOffset now)
    {
        if (IsRunning) return this;
        var left = PausedRemaining is { } paused && paused > TimeSpan.Zero ? paused : Total;
        return this with { EndsAt = now + left, PausedRemaining = null };
    }

    public CountdownTimer Pause(DateTimeOffset now) => IsRunning ? this with { EndsAt = null, PausedRemaining = Remaining(now) } : this;

    public CountdownTimer Reset() => this with { EndsAt = null, PausedRemaining = null };

    public CountdownTimer WithTotal(int seconds) => new(seconds, null, null);
}

/// <summary>A stopwatch: time from earlier runs plus the current run (F-TOOL-02).</summary>
public sealed record StopwatchState(DateTimeOffset? StartedAt, long AccumulatedMs)
{
    public static readonly StopwatchState Default = new(null, 0);

    public bool IsRunning => StartedAt is not null;

    public TimeSpan Elapsed(DateTimeOffset now) =>
        TimeSpan.FromMilliseconds(AccumulatedMs) + (StartedAt is { } start && now > start ? now - start : TimeSpan.Zero);

    public StopwatchState Start(DateTimeOffset now) => IsRunning ? this : this with { StartedAt = now };

    public StopwatchState Pause(DateTimeOffset now) =>
        IsRunning ? new StopwatchState(null, (long)Elapsed(now).TotalMilliseconds) : this;

    public StopwatchState Reset() => Default;
}

public enum PomodoroPhase { Focus, Break, LongBreak }

/// <summary>
/// Pomodoro (F-TOOL-03): 25 minutes of focus, a 5-minute break, and a 15-minute break after the fourth session.
/// </summary>
public sealed record PomodoroState(PomodoroPhase Phase, int Round, CountdownTimer Clock)
{
    public static readonly TimeSpan FocusLength = TimeSpan.FromMinutes(25);
    public static readonly TimeSpan BreakLength = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan LongBreakLength = TimeSpan.FromMinutes(15);
    public const int Rounds = 4;

    public static readonly PomodoroState Default = new(PomodoroPhase.Focus, 1, Timer(PomodoroPhase.Focus));

    public bool IsFocusRunning => Phase == PomodoroPhase.Focus && Clock.IsRunning;

    public PomodoroState Start(DateTimeOffset now) => this with { Clock = Clock.Start(now) };

    public PomodoroState Pause(DateTimeOffset now) => this with { Clock = Clock.Pause(now) };

    public PomodoroState Reset() => Default;

    /// <summary>Focus leads to a break (a long one after the fourth); a break leads to the next session's focus.</summary>
    public PomodoroState NextPhase(DateTimeOffset now, bool startNow)
    {
        var (phase, round) = Phase switch
        {
            PomodoroPhase.Focus => (Round >= Rounds ? PomodoroPhase.LongBreak : PomodoroPhase.Break, Round),
            PomodoroPhase.Break => (PomodoroPhase.Focus, Round + 1),
            _ => (PomodoroPhase.Focus, 1),
        };
        var clock = Timer(phase);
        return new PomodoroState(phase, round, startNow ? clock.Start(now) : clock);
    }

    /// <summary>Skips the break: the next session's focus starts at once (plan 2b decision 5).</summary>
    public PomodoroState Skip(DateTimeOffset now) =>
        new(PomodoroPhase.Focus, Round % Rounds + 1, Timer(PomodoroPhase.Focus).Start(now));

    private static CountdownTimer Timer(PomodoroPhase phase) => CountdownTimer.Default.WithTotal((int)(phase switch
    {
        PomodoroPhase.Focus => FocusLength,
        PomodoroPhase.Break => BreakLength,
        _ => LongBreakLength,
    }).TotalSeconds);
}
