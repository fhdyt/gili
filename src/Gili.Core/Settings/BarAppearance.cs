namespace Gili.Core.Settings;

/// <summary>The material the bar actually draws: Windows' "Transparency effects" switch overrides the user's choice (F-BAR-06).</summary>
public readonly record struct BarAppearance(GlassLevel Glass, double TintOpacity)
{
    public static BarAppearance Resolve(AppSettings settings, bool transparencyEffects) =>
        transparencyEffects
            ? new BarAppearance(settings.Glass, settings.BarOpacity / 100.0)
            : new BarAppearance(GlassLevel.Off, 1.0);
}
