using Gili.Core.Power;
using Windows.System.Power;

namespace Gili.Platform.Power;

/// <summary>Battery and charger state from PowerManager (PRD 9.2).</summary>
public sealed class PowerMonitor : IDisposable
{
    public PowerMonitor()
    {
        PowerManager.BatteryStatusChanged += OnChanged;
        PowerManager.PowerSupplyStatusChanged += OnChanged;
        PowerManager.RemainingChargePercentChanged += OnChanged;
    }

    /// <summary>Raised on a background thread.</summary>
    public event Action<PowerSnapshot>? Changed;

    public static PowerSnapshot Read() => new(
        HasBattery: PowerManager.BatteryStatus != BatteryStatus.NotPresent,
        PluggedIn: PowerManager.PowerSupplyStatus != PowerSupplyStatus.NotPresent,
        Charging: PowerManager.BatteryStatus == BatteryStatus.Charging,
        Percent: PowerManager.RemainingChargePercent);

    /// <summary>Windows' estimate of the time left on battery, or null while it has none.</summary>
    public static TimeSpan? RemainingDischargeTime()
    {
        var left = PowerManager.RemainingDischargeTime;
        return left > TimeSpan.Zero && left < TimeSpan.FromDays(2) ? left : null;
    }

    public void Dispose()
    {
        PowerManager.BatteryStatusChanged -= OnChanged;
        PowerManager.PowerSupplyStatusChanged -= OnChanged;
        PowerManager.RemainingChargePercentChanged -= OnChanged;
    }

    private void OnChanged(object? sender, object e) => Changed?.Invoke(Read());
}
