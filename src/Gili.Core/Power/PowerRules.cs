using System.Globalization;
using Gili.Core.Island;

namespace Gili.Core.Power;

public sealed record PowerSnapshot(bool HasBattery, bool PluggedIn, bool Charging, int Percent);

public static class PowerRules
{
    /// <summary>The wide notice for plugging the charger in or out (PRD 6.3); nothing on the first reading or without a battery.</summary>
    public static WideEvent? WideFor(PowerSnapshot? previous, PowerSnapshot current)
    {
        if (previous is null || !current.HasBattery || previous.PluggedIn == current.PluggedIn) return null;
        var percent = new WideRun(Percent(current.Percent), WideRunStyle.Strong);
        return current.PluggedIn
            ? new WideEvent("charger", WideIcon.Bolt, WideTone.Accent, [new WideRun("Charging · "), percent], TimeSpan.FromSeconds(3))
            : new WideEvent("charger", WideIcon.Bolt, WideTone.Text, [new WideRun("On battery · "), percent], TimeSpan.FromSeconds(2));
    }

    /// <summary>The slot shows a bolt and the percentage while the battery is charging (PRD 6.3, activity 8).</summary>
    public static bool IsCharging(PowerSnapshot power) => power.HasBattery && power.Charging;

    internal static string Percent(int percent) => percent.ToString(CultureInfo.InvariantCulture) + "%";
}
