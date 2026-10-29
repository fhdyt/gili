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

    public static string PomodoroSub(PomodoroState pomodoro, PomodoroPlan plan) =>
        $"{(pomodoro.Phase == PomodoroPhase.Focus ? "Focus" : "Break")} · session {pomodoro.Round} of {plan.Rounds}";

    public static string Button(bool running, bool started) => running ? "Pause" : started ? "Resume" : "Start";

    public static string TimerDone(int totalSeconds) => $"Timer {Clock(TimeSpan.FromSeconds(totalSeconds))} done";

    /// <summary>The focus that just ended, and the break that comes next.</summary>
    public static string FocusDone(PomodoroState focus, PomodoroPlan plan)
    {
        var minutes = focus.Round >= plan.Rounds ? plan.LongBreakMinutes : plan.BreakMinutes;
        return $"{minutes}-minute break · session {focus.Round} of {plan.Rounds}";
    }

    public static string BreakOver(PomodoroState rest, PomodoroPlan plan) =>
        $"Session {(rest.Phase == PomodoroPhase.LongBreak ? 1 : rest.Round + 1)} of {plan.Rounds} ready";

    /// <summary>Clock, with hours (h:mm:ss) from one hour on.</summary>
    public static string Long(TimeSpan time)
    {
        var seconds = (long)Math.Max(0, Math.Ceiling(time.TotalSeconds));
        return seconds < 3600 ? Clock(time) : $"{seconds / 3600}:{Pad(seconds % 3600 / 60)}:{Pad(seconds % 60)}";
    }

    /// <summary>
    /// Reads a typed duration: "5" is minutes, "1:30" minutes and seconds, "1:00:00" hours, minutes and seconds. Null for
    /// anything else, and for zero or 24 hours and more (F-TOOL-01).
    /// </summary>
    public static int? ParseDuration(string text)
    {
        var parts = text.Trim().Split(':');
        if (parts.Length > 3 || parts.Any(p => p.Length is 0 or > 6 || !p.All(char.IsAsciiDigit))) return null;
        var n = parts.Select(p => long.Parse(p, CultureInfo.InvariantCulture)).ToArray();
        long seconds;
        switch (n.Length)
        {
            case 1:
                seconds = n[0] * 60;
                break;
            case 2:
                if (n[1] >= 60) return null;
                seconds = n[0] * 60 + n[1];
                break;
            default:
                if (n[1] >= 60 || n[2] >= 60) return null;
                seconds = n[0] * 3600 + n[1] * 60 + n[2];
                break;
        }
        return seconds is >= 1 and < 86400 ? (int)seconds : null;
    }

    /// <summary>A duration as the Duration field shows it: 5:00, or 1:00:00 from one hour.</summary>
    public static string DurationInput(int seconds) =>
        seconds >= 3600
            ? $"{seconds / 3600}:{Pad(seconds % 3600 / 60)}:{Pad(seconds % 60)}"
            : $"{seconds / 60}:{Pad(seconds % 60)}";

    /// <summary>The detail under a Tools timer row: Ready · 10:00, Running · 10:00 or Paused · 10:00.</summary>
    public static string RowDetail(CountdownTimer timer) =>
        $"{(timer.IsRunning ? "Running" : timer.IsPaused ? "Paused" : "Ready")} · {Long(timer.Total)}";

    public static string LabeledDone(string label) => $"{label} done";

    public static string LabeledSub(int totalSeconds) => $"Timer {Long(TimeSpan.FromSeconds(totalSeconds))}";

    public static string LapName(int n) => $"Lap {n}";

    public static string Minutes(int n) => $"{n} min";

    public static string Sessions(int n) => $"{n} sessions";

    private static string Pad(long value) => value.ToString("00", CultureInfo.InvariantCulture);
}
