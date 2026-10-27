using System.Globalization;

namespace Gili.Core.Timers;

/// <summary>The Timer page, slot and card texts, exactly as the mockup writes them (renderTimerPage, CARDS).</summary>
public static class TimerText
{
    /// <summary>The mockup's mmss(): seconds rounded up, never below zero.</summary>
    public static string Clock(TimeSpan time)
    {
        var seconds = (long)Math.Max(0, Math.Ceiling(time.TotalSeconds));
        return Pad(seconds / 60) + ":" + Pad(seconds % 60);
    }

    /// <summary>Whole seconds, plus tenths on the Timer page.</summary>
    public static string Stopwatch(TimeSpan elapsed, bool tenths)
    {
        var ms = (long)Math.Max(0, elapsed.TotalMilliseconds);
        var text = Clock(TimeSpan.FromSeconds(ms / 1000));
        return tenths ? text + "." + (ms % 1000 / 100).ToString(CultureInfo.InvariantCulture) : text;
    }

    public static string TimerSub(CountdownTimer timer) => timer.IsRunning ? "Running" : timer.IsPaused ? "Paused" : "Pick a duration";

    public static string StopwatchSub(StopwatchState stopwatch) => stopwatch.IsRunning ? "Running" : "Stopwatch";

    public static string PomodoroSub(PomodoroState pomodoro) =>
        $"{(pomodoro.Phase == PomodoroPhase.Focus ? "Focus" : "Break")} · session {pomodoro.Round} of {PomodoroState.Rounds}";

    public static string Button(bool running, bool started) => running ? "Pause" : started ? "Resume" : "Start";

    public static string TimerDone(int totalSeconds) => $"Timer {Clock(TimeSpan.FromSeconds(totalSeconds))} done";

    /// <summary>The focus that just ended, and the break that comes next.</summary>
    public static string FocusDone(PomodoroState focus)
    {
        var minutes = (int)(focus.Round >= PomodoroState.Rounds ? PomodoroState.LongBreakLength : PomodoroState.BreakLength).TotalMinutes;
        return $"{minutes}-minute break · session {focus.Round} of {PomodoroState.Rounds}";
    }

    public static string BreakOver(PomodoroState rest) =>
        $"Session {(rest.Phase == PomodoroPhase.LongBreak ? 1 : rest.Round + 1)} of {PomodoroState.Rounds} ready";

    private static string Pad(long value) => value.ToString("00", CultureInfo.InvariantCulture);
}
