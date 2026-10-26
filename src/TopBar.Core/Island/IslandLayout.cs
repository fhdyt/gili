namespace TopBar.Core.Island;

public readonly record struct IslandGeometry(double Width, double Height, double Top, double CornerRadius);

/// <summary>Size and position of the island in each mode (PRD 6.3, 8.3), in DIPs relative to the top of the screen.</summary>
public static class IslandLayout
{
    public const double MinCompactWidth = 200;
    public const double HoverGrow = 16;
    public const double CompactPadding = 28;
    public const double WidePadding = 32;
    public const double FloatTop = 8;
    public const double ScreenMargin = 24;
    public const double BubbleGap = 6;
    public const double CoverMargin = 8;

    public static IslandGeometry For(
        IslandMode mode, IslandPage page, double contentWidth, bool hovered, int barHeight, int islandHeight, double screenWidth)
    {
        var top = (barHeight - islandHeight) / 2.0;
        var radius = islandHeight / 2.0;
        return mode switch
        {
            IslandMode.Wide => new(Math.Max(MinCompactWidth, contentWidth + WidePadding), islandHeight, top, radius),
            IslandMode.Expanded when page == IslandPage.Notes => Floating(960, 500, 32, screenWidth),
            IslandMode.Expanded => Floating(680, 256, 32, screenWidth),
            IslandMode.Card => Floating(560, 104, 28, screenWidth),
            IslandMode.DropZone => Floating(420, 140, 30, screenWidth),
            _ => new(Math.Max(MinCompactWidth, contentWidth + CompactPadding) + (hovered ? HoverGrow : 0), islandHeight, top, radius),
        };
    }

    /// <summary>A bar element fades while the floating island covers it, with 8 DIP to spare on each side (PRD 6.3).</summary>
    public static bool Covers(double islandLeft, double islandRight, double elementLeft, double elementRight) =>
        elementRight > islandLeft - CoverMargin && elementLeft < islandRight + CoverMargin;

    private static IslandGeometry Floating(double width, double height, double radius, double screenWidth) =>
        new(Math.Min(width, screenWidth - ScreenMargin), height, FloatTop, radius);
}
