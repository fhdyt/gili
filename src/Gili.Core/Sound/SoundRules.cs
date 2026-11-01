namespace Gili.Core.Sound;

/// <summary>Which sound a card makes, and when a card warns that nothing will be heard (PRD 6.6).</summary>
public static class SoundRules
{
    public const string Warning = "Sound is off · Windows volume is muted";

    /// <summary>Muted, or so low it would not be heard (under 5%).</summary>
    public static bool WarnMuted(int volumePercent, bool muted) => muted || volumePercent < 5;

    /// <summary>
    /// A timer rings the chosen timer sound until answered; an alarm rings its own sound (Radar by default) and rises
    /// slowly; a pomodoro phase rings Bell once; a task reminder pings once.
    /// </summary>
    public static (string Voice, bool Loop, bool Ramp)? ForCard(string cardId, string timerSound, string? alarmSound = null) => cardId switch
    {
        _ when cardId.StartsWith("alarm:", StringComparison.Ordinal) => (Voices.Find(alarmSound ?? "Radar").Name, true, true),
        "timer" => (Voices.Find(timerSound).Name, true, false),
        _ when cardId.StartsWith("timer:", StringComparison.Ordinal) => (Voices.Find(timerSound).Name, true, false),
        "pomo-focus" or "pomo-break" => ("Bell", false, false),
        _ when cardId.StartsWith("todo:", StringComparison.Ordinal) => ("Ping", false, false),
        "claude" => ("Ping", false, false),
        "claude-reset" or "claude-weekly-reset" => (Voices.Find(timerSound).Name, true, false),
        _ => null,
    };
}
