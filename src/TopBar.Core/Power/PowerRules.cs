using System.Globalization;
using TopBar.Core.Island;

namespace TopBar.Core.Power;

public sealed record PowerSnapshot(bool HasBattery, bool PluggedIn, bool Charging, int Percent);

public static class PowerRules
{
    /// <summary>The wide notice for plugging the charger in or out (PRD 6.3); nothing on the first reading or without a battery.</summary>
    public static WideEvent? WideFor(PowerSnapshot? previous, PowerSnapshot current)
    {
        if (previous is null || !current.HasBattery || previous.PluggedIn == current.PluggedIn) return null;
        var percent = current.Percent.ToString(CultureInfo.InvariantCulture) + "%";
        return current.PluggedIn
            ? new WideEvent("charger", WideIcon.Charging, "Charging", percent, TimeSpan.FromSeconds(3))
            : new WideEvent("charger", WideIcon.OnBattery, "On battery", percent, TimeSpan.FromSeconds(2));
    }

    /// <summary>The slot shows a bolt and the percentage while the battery is charging (PRD 6.3, activity 8).</summary>
    public static bool IsCharging(PowerSnapshot power) => power.HasBattery && power.Charging;
}
