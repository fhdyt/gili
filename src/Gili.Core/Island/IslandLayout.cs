namespace Gili.Core.Island;

public readonly record struct IslandGeometry(double Width, double Height, double Top, double CornerRadius);

/// <summary>The top-left corner of an 8 DIP privacy dot.</summary>
public readonly record struct DotPosition(double X, double Y);

/// <summary>The bar and island heights from Settings and the primary screen's size, in DIPs.</summary>
public readonly record struct IslandScreen(int BarHeight, int IslandHeight, double Width, double Height);

/// <summary>Size and position of the island in each mode (PRD 6.3, 8.3), in DIPs relative to the top of the screen.</summary>
public static class IslandLayout
{
    public const double MinCompactWidth = 200;
    public const double HoverGrow = 16;
    public const double CompactPadding = 28;
    public const double WidePadding = 32;
    public const double ScreenMargin = 24;
    public const double BubbleGap = 6;
    public const double CoverMargin = 8;
    public const double ExpandedMinHeight = 256;

    /// <summary>The open island's page area starts below the tab menu (14 + 36 + 8) and keeps 20 below it.</summary>
    public const double PageTop = 58;
    public const double PageBottom = 20;

    /// <summary>A control island (PRD 6.4) is 360 wide, 400 for the Wi‑Fi and Bluetooth lists, its content 16 from every edge.</summary>
    public const double ControlWidth = 360;
    public const double ListControlWidth = 400;
    public const double ControlPadding = 16;

    public static double ControlWidthFor(ControlKind control) =>
        control is ControlKind.Wifi or ControlKind.Bluetooth ? ListControlWidth : ControlWidth;

    /// <summary>
    /// The open island is <paramref name="expandedWidth"/> wide (from the tab menu) and as tall as its page, up to the
    /// screen with the same gap below as above; a control island has no menu and is as tall as its control. Every
    /// floating state keeps the compact island's top edge.
    /// </summary>
    public static IslandGeometry For(
        IslandMode mode, IslandScreen screen, double contentWidth, bool hovered, double expandedWidth, double pageHeight,
        IslandPage page = IslandPage.Media, ControlKind control = ControlKind.Power)
    {
        var top = (screen.BarHeight - screen.IslandHeight) / 2.0;
        var radius = screen.IslandHeight / 2.0;
        var maxHeight = screen.Height - 2 * top;
        return mode switch
        {
            IslandMode.Expanded when page == IslandPage.Controls => new(
                Math.Min(ControlWidthFor(control), screen.Width - ScreenMargin),
                Math.Min(pageHeight + 2 * ControlPadding, maxHeight),
                top,
                28),
            IslandMode.Wide => new(Math.Max(MinCompactWidth, contentWidth + WidePadding), screen.IslandHeight, top, radius),
            IslandMode.Expanded => new(
                expandedWidth,
                Math.Clamp(PageTop + pageHeight + PageBottom, ExpandedMinHeight, maxHeight),
                top,
                32),
            IslandMode.Card => new(Math.Min(560, screen.Width - ScreenMargin), 104, top, 28),
            IslandMode.DropZone => new(Math.Min(420, screen.Width - ScreenMargin), 140, top, 30),
            _ => new(
                Math.Max(MinCompactWidth, contentWidth + CompactPadding) + (hovered ? HoverGrow : 0),
                screen.IslandHeight,
                top,
                radius),
        };
    }

    /// <summary>
    /// The camera and microphone dots' top-left corners, camera first (PRD 6.3, mockup <c>layout()</c>): a row 6 left of the
    /// island and Gili's circle, 13 apart, centred on the compact island; down the panel's left edge while it floats,
    /// below Gili's circle when that shows beside a card. <paramref name="circleSpace"/> is the circle's width plus its gap
    /// as far as its spring has come.
    /// </summary>
    public static IReadOnlyList<DotPosition> PrivacyDots(
        double islandLeft, double islandTop, double islandHeight, double circleSpace, bool floating, bool circleBesideFloating, int count)
    {
        var dots = new DotPosition[count];
        for (var k = 0; k < count; k++)
        {
            dots[k] = floating
                ? new(islandLeft - BubbleGap - DotSize, islandTop + (circleBesideFloating ? islandHeight + 8 : 20) + k * DotStep)
                : new(islandLeft - BubbleGap - DotSize - circleSpace - k * DotStep, islandTop + islandHeight / 2 - DotSize / 2);
        }
        return dots;
    }

    public const double DotSize = 8;

    /// <summary>A dot and the 5 DIP after it.</summary>
    public const double DotStep = 13;

    /// <summary>A bar element fades while the floating island covers it, with 8 DIP to spare on each side (PRD 6.3).</summary>
    public static bool Covers(double islandLeft, double islandRight, double elementLeft, double elementRight) =>
        elementRight > islandLeft - CoverMargin && elementLeft < islandRight + CoverMargin;
}
