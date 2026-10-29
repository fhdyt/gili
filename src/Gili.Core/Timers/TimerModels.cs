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

/// <summary>A countdown with a name, kept in Tools next to the island's own timer (F-TOOL-01).</summary>
public sealed record LabeledTimer(long Id, string Label, CountdownTimer Clock)
{
    public const int MaxCount = 10;
    public const int LabelLimit = 40;

    /// <summary>Trims and cuts the label; an empty one reads Timer so it never clashes with the island's own timer.</summary>
    public static string CleanLabel(string text)
    {
        var label = text.Trim();
        if (label.Length > LabelLimit) label = label[..LabelLimit].TrimEnd();
        return label.Length == 0 ? "Timer" : label;
    }
}

/// <summary>Which labeled timers have ended and which running timer ends first.</summary>
public static class TimerSchedule
{
    /// <summary>Ids of the timers that have ended, the earliest end first, then by id (cards queue in this order).</summary>
    public static IReadOnlyList<long> Finished(IReadOnlyList<LabeledTimer> timers, DateTimeOffset now) =>
        [.. timers.Where(t => t.Clock.IsDone(now)).OrderBy(t => t.Clock.EndsAt).ThenBy(t => t.Id).Select(t => t.Id)];

    /// <summary>The running timer with the earliest end, or null when none runs (paused timers do not count).</summary>
    public static CountdownTimer? Soonest(IEnumerable<CountdownTimer> running, DateTimeOffset now) =>
        running.Where(t => t.IsRunning).MinBy(t => t.EndsAt);
}

/// <summary>A stopwatch: time from earlier runs plus the current run, and the laps taken so far (F-TOOL-02).</summary>
public sealed record StopwatchState(DateTimeOffset? StartedAt, long AccumulatedMs, IReadOnlyList<long> Laps)
{
    public const int MaxLaps = 99;

    public static readonly StopwatchState Default = new(null, 0, []);

    public bool IsRunning => StartedAt is not null;

    public bool CanLap => IsRunning && Laps.Count < MaxLaps;

    public TimeSpan Elapsed(DateTimeOffset now) =>
        TimeSpan.FromMilliseconds(AccumulatedMs) + (StartedAt is { } start && now > start ? now - start : TimeSpan.Zero);

    public StopwatchState Start(DateTimeOffset now) => IsRunning ? this : this with { StartedAt = now };

    public StopwatchState Pause(DateTimeOffset now) =>
        IsRunning ? this with { StartedAt = null, AccumulatedMs = (long)Elapsed(now).TotalMilliseconds } : this;

    /// <summary>Adds the total elapsed time as a lap; only while running and below 99 laps.</summary>
    public StopwatchState Lap(DateTimeOffset now) =>
        CanLap ? this with { Laps = [.. Laps, (long)Elapsed(now).TotalMilliseconds] } : this;

    public StopwatchState Reset() => Default;

    /// <summary>Records compare lists by reference, so equality looks at the laps themselves.</summary>
    public bool Equals(StopwatchState? other) =>
        other is not null && StartedAt == other.StartedAt && AccumulatedMs == other.AccumulatedMs && Laps.SequenceEqual(other.Laps);

    public override int GetHashCode() => HashCode.Combine(StartedAt, AccumulatedMs, Laps.Count);
}

public enum PomodoroPhase { Focus, Break, LongBreak }

/// <summary>The pomodoro lengths and the number of sessions before the long break (F-TOOL-03); kept in settings.</summary>
public sealed record PomodoroPlan(int FocusMinutes, int BreakMinutes, int LongBreakMinutes, int Rounds)
{
    public static readonly PomodoroPlan Default = new(25, 5, 15, 4);

    /// <summary>Focus 1–120 minutes, breaks 1–60 minutes, 2–8 sessions.</summary>
    public PomodoroPlan Normalize() => new(
        Math.Clamp(FocusMinutes, 1, 120),
        Math.Clamp(BreakMinutes, 1, 60),
        Math.Clamp(LongBreakMinutes, 1, 60),
        Math.Clamp(Rounds, 2, 8));

    public TimeSpan Length(PomodoroPhase phase) => TimeSpan.FromMinutes(phase switch
    {
        PomodoroPhase.Focus => FocusMinutes,
        PomodoroPhase.Break => BreakMinutes,
        _ => LongBreakMinutes,
    });
}

/// <summary>
/// Pomodoro (F-TOOL-03): focus, a short break, and a long break after the last session; the lengths come from the plan.
/// </summary>
public sealed record PomodoroState(PomodoroPhase Phase, int Round, CountdownTimer Clock)
{
    public static readonly PomodoroState Default = Initial(PomodoroPlan.Default);

    public bool IsFocusRunning => Phase == PomodoroPhase.Focus && Clock.IsRunning;

    public static PomodoroState Initial(PomodoroPlan plan) => new(PomodoroPhase.Focus, 1, Timer(plan, PomodoroPhase.Focus));

    public static PomodoroState Reset(PomodoroPlan plan) => Initial(plan);

    public PomodoroState Start(DateTimeOffset now) => this with { Clock = Clock.Start(now) };

    public PomodoroState Pause(DateTimeOffset now) => this with { Clock = Clock.Pause(now) };

    /// <summary>Focus leads to a break (a long one after the last session); a break leads to the next session's focus.</summary>
    public PomodoroState NextPhase(DateTimeOffset now, bool startNow, PomodoroPlan plan)
    {
        var (phase, round) = Phase switch
        {
            PomodoroPhase.Focus => (Round >= plan.Rounds ? PomodoroPhase.LongBreak : PomodoroPhase.Break, Round),
            PomodoroPhase.Break => (PomodoroPhase.Focus, Round + 1),
            _ => (PomodoroPhase.Focus, 1),
        };
        var clock = Timer(plan, phase);
        return new PomodoroState(phase, round, startNow ? clock.Start(now) : clock);
    }

    /// <summary>Skips the break: the next session's focus starts at once (plan 2b decision 5).</summary>
    public PomodoroState Skip(DateTimeOffset now, PomodoroPlan plan) =>
        new(PomodoroPhase.Focus, Round >= plan.Rounds ? 1 : Round + 1, Timer(plan, PomodoroPhase.Focus).Start(now));

    /// <summary>
    /// A new plan applies to the next phases: a running or paused phase keeps its length, a phase not yet started takes
    /// the new one, and the session never exceeds the plan's session count (plan 2g decision 6).
    /// </summary>
    public PomodoroState WithPlan(PomodoroPlan plan)
    {
        var started = Clock.IsRunning || Clock.IsPaused;
        return this with { Round = Math.Min(Round, plan.Rounds), Clock = started ? Clock : Timer(plan, Phase) };
    }

    private static CountdownTimer Timer(PomodoroPlan plan, PomodoroPhase phase) =>
        CountdownTimer.Default.WithTotal((int)plan.Length(phase).TotalSeconds);
}

