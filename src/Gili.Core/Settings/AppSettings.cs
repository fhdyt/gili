namespace Gili.Core.Settings;

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
    public int BarOpacity { get; init; } = 100;
    public GlassLevel Glass { get; init; } = GlassLevel.Strong;
    public int BarHeight { get; init; } = 32;
    public int IslandHeight { get; init; } = 24;
    public bool Autostart { get; init; } = true;
    public bool ShowSeconds { get; init; } = true;
    public bool ShowGili { get; init; } = true;
    public bool AmbientGlow { get; init; } = true;
    public bool AmbientMotion { get; init; } = true;

    /// <summary>The timer's sound, the volume of Gili's own sounds (0–100) and the alarm's slow rise (PRD 6.6).</summary>
    public string TimerSound { get; init; } = "Chime";
    public int AlertVolume { get; init; } = 80;
    public bool GraduallyIncrease { get; init; } = true;

    /// <summary>The weather city until Settings offers a choice (plan 1e decision 1).</summary>
    public string WeatherCity { get; init; } = "Jakarta";
    public double WeatherLatitude { get; init; } = -6.2088;
    public double WeatherLongitude { get; init; } = 106.8456;

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
            AlertVolume = Math.Clamp(AlertVolume, 0, 100),
            TimerSound = Sound.Voices.Find(TimerSound).Name,
        };
    }
}
