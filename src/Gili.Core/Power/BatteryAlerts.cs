using Gili.Core.Island;

namespace Gili.Core.Power;

/// <summary>
/// The charger and battery notices (PRD 6.3, F-ISL-11), each once per cycle: low and critical once per discharge, on hold,
/// the unplug reminder and full once per plug. A laptop with a charge limit stops below 100 % and never passes the
/// reminder, so it gets "Charging on hold" instead of a wrong "unplug" (plan 3a).
/// </summary>
public sealed class BatteryAlerts
{
    private const int Critical = 10;
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(5);

    private PowerSnapshot? _previous;
    private bool _lowShown;
    private bool _criticalShown;
    private bool _holdShown;
    private bool _unplugShown;
    private bool _fullShown;

    /// <summary>The notices for a new reading, in the order they show.</summary>
    public IReadOnlyList<WideEvent> Update(PowerSnapshot current, int lowAt, int unplugAt)
    {
        var previous = _previous;
        _previous = current;
        if (!current.HasBattery) return [];
        var notices = new List<WideEvent>();
        if (previous is null || !previous.HasBattery)
        {
            // Starting up: a battery already low warns once; a plugged-in laptop starts quiet.
            if (current.PluggedIn) Settle(current, unplugAt);
            else AddLow(current, lowAt, notices);
            return notices;
        }
        if (previous.PluggedIn != current.PluggedIn)
        {
            _holdShown = _unplugShown = _fullShown = false;
            if (current.PluggedIn)
            {
                _lowShown = _criticalShown = false;
                if (current.OnHold)
                {
                    _holdShown = true;
                    notices.Add(Hold("charger", current.Percent));
                }
                else if (PowerRules.WideFor(previous, current) is { } charger) notices.Add(charger);
                _fullShown = current.Percent >= 100;
                return notices;
            }
            if (PowerRules.WideFor(previous, current) is { } battery) notices.Add(battery);
        }
        if (current.PluggedIn) AddCharging(current, unplugAt, notices);
        else AddLow(current, lowAt, notices);
        return notices;
    }

    private void Settle(PowerSnapshot current, int unplugAt)
    {
        _holdShown = current.OnHold;
        _unplugShown = current.Percent > unplugAt;
        _fullShown = current.Percent >= 100;
    }

    private void AddLow(PowerSnapshot current, int lowAt, List<WideEvent> notices)
    {
        if (current.Percent <= Critical && !_criticalShown)
        {
            _criticalShown = _lowShown = true;
            notices.Add(new("battery", WideIcon.Bolt, WideTone.Danger,
                [new WideRun("Battery "), new WideRun(PowerRules.Percent(current.Percent), WideRunStyle.Strong)], Duration));
        }
        else if (current.Percent <= lowAt && !_lowShown)
        {
            _lowShown = true;
            notices.Add(new("battery", WideIcon.Bolt, WideTone.Warn,
                [new WideRun("Battery "), new WideRun(PowerRules.Percent(current.Percent), WideRunStyle.Strong), new WideRun(" · plug in")],
                Duration));
        }
    }

    private void AddCharging(PowerSnapshot current, int unplugAt, List<WideEvent> notices)
    {
        if (current.OnHold && !_holdShown)
        {
            _holdShown = true;
            notices.Add(Hold("battery", current.Percent));
        }
        else if (current.Charging && unplugAt < 100 && current.Percent > unplugAt && !_unplugShown)
        {
            _unplugShown = true;
            notices.Add(new("battery", WideIcon.Bolt, WideTone.Accent,
                [new WideRun(PowerRules.Percent(unplugAt), WideRunStyle.Strong), new WideRun(" reached · unplug")], Duration));
        }
        else if (current.Percent >= 100 && !_fullShown)
        {
            _fullShown = true;
            notices.Add(new("battery", WideIcon.Bolt, WideTone.Accent, [new WideRun("Fully charged")], Duration));
        }
    }

    private static WideEvent Hold(string key, int percent) =>
        new(key, WideIcon.Bolt, WideTone.Accent,
            [new WideRun("Charging on hold · "), new WideRun(PowerRules.Percent(percent), WideRunStyle.Strong)], Duration);
}
