using System.Windows.Threading;
using Gili.Core.Formatting;

namespace Gili.App;

/// <summary>Ticks exactly when the minute changes (F-ISL-01). Call Refresh after a clock, time zone or resume change.</summary>
public sealed class ClockService
{
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Normal);

    public ClockService()
    {
        _timer.Tick += (_, _) => Refresh();
        Refresh();
    }

    public DateTime Now { get; private set; }

    public event Action? Ticked;

    public void Refresh()
    {
        Now = DateTime.Now;
        _timer.Stop();
        _timer.Interval = ClockText.UntilNextMinute(Now) + TimeSpan.FromMilliseconds(50);
        _timer.Start();
        Ticked?.Invoke();
    }
}
