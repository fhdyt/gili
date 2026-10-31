namespace Gili.Core.Timers;

/// <summary>What an edit does to the card a labeled timer may be showing.</summary>
public enum CardEffect { None, Refresh, Dismiss }

/// <summary>When a labeled timer's card still applies after Tools changes the timer (plan 2g decision 3, 4).</summary>
public static class TimerCardRules
{
    /// <summary>A new duration closes the card (and wins over a new label); only a new label refreshes it; otherwise nothing.</summary>
    public static CardEffect ForEdit(LabeledTimer before, string newLabel, int newSeconds)
    {
        if (newSeconds != before.Clock.TotalSeconds) return CardEffect.Dismiss;
        return LabeledTimer.CleanLabel(newLabel) != before.Label ? CardEffect.Refresh : CardEffect.None;
    }

    /// <summary>Starting answers the card; pausing does not.</summary>
    public static bool StartOrPauseDismisses(CountdownTimer before) => !before.IsRunning;

    /// <summary>Reset always answers the card.</summary>
    public static bool ResetDismisses => true;
}
