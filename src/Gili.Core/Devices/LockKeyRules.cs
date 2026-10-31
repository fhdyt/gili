using Gili.Core.Island;

namespace Gili.Core.Devices;

/// <summary>The toggle state of Caps Lock and Num Lock.</summary>
public readonly record struct LockKeys(bool Caps, bool Num);

/// <summary>The wide notices for pressing Caps Lock or Num Lock (PRD 6.3 "Kejadian wide", F-ISL-10).</summary>
public static class LockKeyRules
{
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(1.5);

    /// <summary>Caps first when both changed between two readings; nothing on the first reading.</summary>
    public static IReadOnlyList<WideEvent> WideFor(LockKeys? previous, LockKeys current)
    {
        if (previous is not { } before) return [];
        var notices = new List<WideEvent>();
        if (before.Caps != current.Caps) notices.Add(Notice("caps", WideIcon.Caps, "Caps Lock ", current.Caps));
        if (before.Num != current.Num) notices.Add(Notice("num", WideIcon.NumLock, "Num Lock ", current.Num));
        return notices;
    }

    private static WideEvent Notice(string key, WideIcon icon, string name, bool on) =>
        new(key, icon, WideTone.Text, [new WideRun(name), new WideRun(on ? "on" : "off", WideRunStyle.Strong)], Duration);
}
