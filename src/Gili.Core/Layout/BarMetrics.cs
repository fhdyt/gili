namespace Gili.Core.Layout;

/// <summary>Sizes derived from the bar and island heights (PRD 6.1, 6.4, 8.3).</summary>
public static class BarMetrics
{
    public const double IslandMinWidth = 200;

    public static double IslandFontSize(int islandHeight) => Math.Clamp(islandHeight / 2.0, 11, 15);

    public static double IslandCornerRadius(int islandHeight) => islandHeight / 2.0;

    public static double ModuleHitHeight(int barHeight) => Math.Max(24, barHeight - 6);
}
