using System.Windows.Threading;
using Gili.Core.Alarms;
using Gili.Data;
using Gili.Platform.Power;

namespace Gili.App.Island;

/// <summary>
/// The alarms (F-TOOL-05): kept in gili.db with their next ring in UTC, one timer to the soonest, and a Windows wake
/// timer so a sleeping laptop wakes for it. Times follow the local zone and are worked out again on start, on waking
/// and when the clock or zone changes. Raised on the UI thread.
/// </summary>
public sealed class AlarmService : IDisposable
{
    private readonly AlarmStore? _store;
    private readonly WakeTimer _wake;
    private readonly DispatcherTimer _deadline = new();
    // Snoozed alarms keep their +5 min when times are worked out again; a restart drops the snooze.
    private readonly HashSet<long> _snoozed = [];
    private List<Alarm> _alarms;

    public AlarmService(AlarmStore? store, WakeTimer wake)
    {
        _store = store;
        _wake = wake;
        _alarms = [.. store?.All() ?? []];
        _deadline.Tick += (_, _) => Reschedule();
    }

    public IReadOnlyList<Alarm> Alarms => _alarms;

    public Alarm? Next => AlarmSchedule.Next(_alarms);

    public event Action? Changed;

    /// <summary>An alarm reached its time; true when it is late (missed while asleep or closed).</summary>
    public event Action<Alarm, bool>? Ringing;

    /// <summary>An alarm was changed, turned off or deleted: a card it raised no longer applies.</summary>
    public event Action<long>? Stale;

    private static DateTimeOffset Now => DateTimeOffset.UtcNow;

    public Alarm Add(Alarm alarm)
    {
        var armed = AlarmSchedule.Arm(alarm with { Enabled = true }, Now, TimeZoneInfo.Local);
        armed = _store?.Add(armed) ?? armed with { Id = _alarms.Count == 0 ? 1 : _alarms.Max(a => a.Id) + 1 };
        _alarms = [.. _alarms.Append(armed).OrderBy(a => a.Time).ThenBy(a => a.Id)];
        Saved();
        return armed;
    }

    public void Update(Alarm alarm)
    {
        _snoozed.Remove(alarm.Id);
        Replace(AlarmSchedule.Arm(alarm, Now, TimeZoneInfo.Local));
        Stale?.Invoke(alarm.Id);
    }

    public void Delete(long id)
    {
        _store?.Delete(id);
        _snoozed.Remove(id);
        _alarms.RemoveAll(a => a.Id == id);
        Stale?.Invoke(id);
        Saved();
    }

    public void SetEnabled(long id, bool enabled)
    {
        if (Find(id) is not { } alarm || alarm.Enabled == enabled) return;
        _snoozed.Remove(id);
        Replace(AlarmSchedule.Arm(alarm with { Enabled = enabled }, Now, TimeZoneInfo.Local));
        if (!enabled) Stale?.Invoke(id);
    }

    /// <summary>Rings again in 5 minutes; a one-off alarm stays on for it.</summary>
    public void Snooze(long id)
    {
        if (Find(id) is not { } alarm) return;
        _snoozed.Add(id);
        Replace(AlarmSchedule.Snooze(alarm with { Enabled = true }, Now));
    }

    /// <summary>
    /// Rings whatever has come due (late ones as missed), moves each to its next time, then waits for the soonest and
    /// sets the wake timer. Called at start, on waking and when the clock or time zone changes.
    /// </summary>
    public void Reschedule()
    {
        var now = Now;
        var due = AlarmSchedule.Due(_alarms, now);
        var rang = due.Select(d => d.Alarm.Id).ToHashSet();
        _snoozed.ExceptWith(rang);
        // Rung alarms move on; the rest follow a changed clock or zone, except a snooze.
        _alarms = [.. _alarms.Select(a => rang.Contains(a.Id) ? AlarmSchedule.AfterRing(a, now, TimeZoneInfo.Local)
            : _snoozed.Contains(a.Id) ? a : AlarmSchedule.Arm(a, now, TimeZoneInfo.Local))];
        Saved();
        foreach (var (alarm, missed) in due) Ringing?.Invoke(alarm, missed);
    }

    public void Dispose()
    {
        _deadline.Stop();
        _wake.Dispose();
    }

    private Alarm? Find(long id) => _alarms.FirstOrDefault(a => a.Id == id);

    private void Replace(Alarm alarm)
    {
        var index = _alarms.FindIndex(a => a.Id == alarm.Id);
        if (index < 0) return;
        _alarms[index] = alarm;
        _alarms = [.. _alarms.OrderBy(a => a.Time).ThenBy(a => a.Id)];
        Saved();
    }

    /// <summary>Stores the list, points the timer and the wake timer at the soonest alarm, and says so.</summary>
    private void Saved()
    {
        foreach (var alarm in _alarms) _store?.Update(alarm);
        var next = Next?.NextFireAt;
        _wake.Set(next);
        _deadline.Stop();
        if (next is { } at)
        {
            var wait = at - Now;
            _deadline.Interval = wait > TimeSpan.FromMilliseconds(10) ? wait : TimeSpan.FromMilliseconds(10);
            _deadline.Start();
        }
        Changed?.Invoke();
    }
}
