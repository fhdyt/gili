namespace TopBar.Core.Settings;

public enum GlassLevel { Off, Light, Strong }

/// <summary>Everything stored in settings.json. Later phases add fields with their own defaults.</summary>
public sealed record AppSettings
{
    public const int MinBarHeight = 28;
    public const int MaxBarHeight = 44;
    public const int MinIslandHeight = 20;
    public const int MinBarOpacity = 40;
    public const int MaxBarOpacity = 100;

    public bool Use24HourClock { get; init; } = true;
    public int BarOpacity { get; init; } = 78;
    public GlassLevel Glass { get; init; } = GlassLevel.Strong;
    public int BarHeight { get; init; } = 32;
    public int IslandHeight { get; init; } = 24;
    public bool Autostart { get; init; } = true;

    public static int MaxIslandHeight(int barHeight) => barHeight - 4;

    /// <summary>Clamps every value to the ranges in PRD 6.1 and F-BAR-08.</summary>
    public AppSettings Normalize()
    {
        var barHeight = Math.Clamp(BarHeight, MinBarHeight, MaxBarHeight);
        return this with
        {
            BarHeight = barHeight,
            IslandHeight = Math.Clamp(IslandHeight, MinIslandHeight, MaxIslandHeight(barHeight)),
            BarOpacity = Math.Clamp(BarOpacity, MinBarOpacity, MaxBarOpacity),
            Glass = Enum.IsDefined(Glass) ? Glass : GlassLevel.Strong,
        };
    }
}
