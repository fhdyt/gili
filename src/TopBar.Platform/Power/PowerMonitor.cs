using TopBar.Core.Power;
using Windows.System.Power;

namespace TopBar.Platform.Power;

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

    public void Dispose()
    {
        PowerManager.BatteryStatusChanged -= OnChanged;
        PowerManager.PowerSupplyStatusChanged -= OnChanged;
        PowerManager.RemainingChargePercentChanged -= OnChanged;
    }

    private void OnChanged(object? sender, object e) => Changed?.Invoke(Read());
}
