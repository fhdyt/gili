namespace Gili.Core.Settings;

/// <summary>
/// The material the bar actually draws: Windows' "Transparency effects" switch overrides the user's choice (F-BAR-06).
/// <see cref="Color"/> is the tint's <c>#RRGGBB</c> (F-BAR-14).
/// </summary>
public readonly record struct BarAppearance(GlassLevel Glass, double TintOpacity, string Color)
{
    public static BarAppearance Resolve(AppSettings settings, bool transparencyEffects) =>
        transparencyEffects
            ? new BarAppearance(settings.Glass, settings.BarOpacity / 100.0, settings.BarColor)
            : new BarAppearance(GlassLevel.Off, 1.0, settings.BarColor);

    /// <summary>
    /// How much of the bar's backdrop (tint, glass, hairline) shows: all of it, except while the island floats and, with
    /// Same on the desktop, while the desktop is in front; then Opacity while open (F-BAR-15, F-BAR-16; revision 14).
    /// </summary>
    public static double Backdrop(AppSettings settings, bool islandFloating, bool desktopInFront) =>
        islandFloating || (desktopInFront && settings.SameOnDesktop) ? settings.OpenOpacity / 100.0 : 1.0;
}
