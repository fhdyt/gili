using System.Windows.Threading;
using Gili.Core.Formatting;

namespace Gili.App;

/// <summary>
/// Ticks exactly when the minute changes (F-ISL-01) and, while seconds show, when each second starts (F-RIGHT-11).
/// Call Refresh after a clock, time zone or resume change.
/// </summary>
public sealed class ClockService
{
    private readonly DispatcherTimer _minute = new(DispatcherPriority.Normal);
    private readonly DispatcherTimer _second = new(DispatcherPriority.Render);
    private bool _seconds;

    public ClockService()
    {
        _minute.Tick += (_, _) => Refresh();
        _second.Tick += (_, _) => TickSecond();
        Refresh();
    }

    public DateTime Now { get; private set; }

    public event Action? Ticked;

    public event Action? SecondTicked;

    /// <summary>Seconds are read only while the bar shows them (Settings → General → Show seconds).</summary>
    public bool Seconds
    {
        get => _seconds;
        set
        {
            if (_seconds == value) return;
            _seconds = value;
            if (value) ArmSecond(DateTime.Now);
            else _second.Stop();
        }
    }

    public void Refresh()
    {
        Now = DateTime.Now;
        _minute.Stop();
        _minute.Interval = ClockText.UntilNextMinute(Now) + TimeSpan.FromMilliseconds(50);
        _minute.Start();
        if (_seconds) ArmSecond(Now);
        Ticked?.Invoke();
    }

    private void TickSecond()
    {
        var now = DateTime.Now;
        Now = now;
        ArmSecond(now);
        SecondTicked?.Invoke();
    }

    /// <summary>Re-armed every tick from the real time, so the seconds never drift from the system clock.</summary>
    private void ArmSecond(DateTime now)
    {
        _second.Stop();
        _second.Interval = ClockText.UntilNextSecond(now) + TimeSpan.FromMilliseconds(15);
        _second.Start();
    }
}
