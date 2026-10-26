namespace Gili.Core.Island;

/// <summary>A tab name's width at 12 px, regular (hovered) and semibold (active), as the app measures it.</summary>
public readonly record struct TabName(double Regular, double Bold);

/// <summary>
/// The open island's icon tab menu (PRD 6.3 "Menu halaman", mockup <c>menuNeed()</c> and <c>placeInd()</c>): icon tabs are
/// 36 px, a tab showing its name adds 8 px of padding and gap plus the name, and the menu's leftover width is shared evenly.
/// </summary>
public static class TabMenuLayout
{
    public const double MinIslandWidth = 560;
    public const double IconTab = 36;
    public const double OpenExtra = 8;
    public const double Gap = 2;
    public const double Padding = 3;

    /// <summary>
    /// The island width the menu needs: 14 px insets, Gili's circle and its 6 px gap, the menu padding, n icons with 2 px
    /// gaps, and room for two names (the active tab and the hovered one), so opening a name never widens the island.
    /// </summary>
    public static double Need(int tabCount, bool showGili, double longestBold) =>
        14 + (showGili ? 42 : 0) + 2 * Padding + tabCount * IconTab + (tabCount - 1) * Gap + 2 * (OpenExtra + longestBold) + 14;

    public static double IslandWidth(double need, double screenWidth) =>
        Math.Min(Math.Max(MinIslandWidth, need), screenWidth - IslandLayout.ScreenMargin);

    /// <summary>Too narrow for a second name: the hovered tab stays an icon and its name becomes a tooltip.</summary>
    public static bool IsTight(double need, double islandWidth) => need > islandWidth;

    /// <summary>Each tab's width once every name has finished opening or closing, the way the menu's flex layout settles.</summary>
    public static double[] TabWidths(double menuWidth, IReadOnlyList<TabName> names, int active, int hovered, bool tight)
    {
        var widths = new double[names.Count];
        for (var i = 0; i < names.Count; i++)
        {
            widths[i] = i == active ? IconTab + OpenExtra + names[i].Bold
                : i == hovered && !tight ? IconTab + OpenExtra + names[i].Regular
                : IconTab;
        }
        var sum = widths.Sum();
        var left = menuWidth - 2 * Padding - (names.Count - 1) * Gap - sum;
        for (var i = 0; i < widths.Length; i++)
        {
            widths[i] = left >= 0 ? widths[i] + left / widths.Length : widths[i] + left * widths[i] / sum;
        }
        return widths;
    }

    /// <summary>The highlight's left edge inside the menu and its width: it sits exactly on the active tab.</summary>
    public static (double X, double Width) Highlight(IReadOnlyList<double> widths, int active)
    {
        var x = Padding;
        for (var i = 0; i < active; i++) x += widths[i] + Gap;
        return (x, widths[active]);
    }
}
