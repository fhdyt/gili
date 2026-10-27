namespace Gili.Core.Modules;

/// <summary>
/// Whether the bar's timers may poll: not while the screen is off, a fullscreen app hides the bar, or the laptop
/// sleeps (PRD 8.1). Changed fires only when the answer changes.
/// </summary>
public sealed class PollingGate
{
    private bool _screenOff;
    private bool _fullscreen;
    private bool _suspended;

    public bool ShouldPoll => !_screenOff && !_fullscreen && !_suspended;

    public event Action<bool>? Changed;

    /// <summary>What holds polling, for the log: "screen off", "fullscreen", "sleep", or empty.</summary>
    public string Reason => string.Join(", ", new[] { (_screenOff, "screen off"), (_fullscreen, "fullscreen"), (_suspended, "sleep") }
        .Where(r => r.Item1).Select(r => r.Item2));

    public void SetScreenOn(bool on) => Set(ref _screenOff, !on);

    public void SetFullscreen(bool fullscreen) => Set(ref _fullscreen, fullscreen);

    public void SetSuspended(bool suspended) => Set(ref _suspended, suspended);

    private void Set(ref bool field, bool value)
    {
        var before = ShouldPoll;
        field = value;
        if (ShouldPoll != before) Changed?.Invoke(ShouldPoll);
    }
}
