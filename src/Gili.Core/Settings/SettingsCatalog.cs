using System.Globalization;

namespace Gili.Core.Settings;

public enum SettingKind { Switch, Segment, Range }

/// <summary>
/// One row of the Settings window (PRD 6.5). <paramref name="Default"/> is what a row that is not available yet shows:
/// a bool for a switch, an option for a segment, a number for a range.
/// </summary>
public sealed record SettingRow(
    string Key,
    string Label,
    SettingKind Kind,
    string? Help = null,
    bool Available = false,
    IReadOnlyList<string>? Options = null,
    string Unit = "",
    object? Default = null);

public sealed record SettingsSection(string Name, IReadOnlyList<SettingRow> Rows);

/// <summary>
/// The Settings window's sections and rows as in the mockup (<c>SET</c>) and PRD 6.5, and how the rows that already
/// work map onto <see cref="AppSettings"/>. Rows of later phases show their PRD default, greyed out.
/// </summary>
public static class SettingsCatalog
{
    public const string LaterVersion = "Coming in a later version";

    private static readonly string[] LeftIcons = ["Start", "Timer", "Alarms", "Todo", "Notes", "Clipboard", "Shelf", "Claude"];
    private static readonly string[] Pages = ["Media", "Timer", "Alarms", "Calendar", "Todo", "Notes", "Clipboard", "Shelf", "Claude", "System"];

    public static IReadOnlyList<SettingsSection> Sections { get; } =
    [
        new("General",
        [
            new("pos", "Bar position", SettingKind.Segment, Options: ["Top", "Bottom"], Default: "Top"),
            new("hideTb", "Hide Windows taskbar", SettingKind.Switch, "Gili gives you Start, tray icons and Controls instead", Default: false),
            new("h12", "12-hour clock", SettingKind.Switch, Available: true),
            new("secs", "Show seconds", SettingKind.Switch, "The clock on the bar ticks every second", Available: true),
            new("auto", "Start with Windows", SettingKind.Switch, Available: true),
        ]),
        new("Appearance",
        [
            new("opacity", "Bar opacity", SettingKind.Range, Available: true, Unit: "%"),
            new("glass", "Glass", SettingKind.Segment, Available: true, Options: ["Off", "Light", "Strong"]),
            new("barH", "Bar height", SettingKind.Range, Available: true, Unit: " px"),
            new("islH", "Island height", SettingKind.Range, "From 20 px up to the bar height minus 4 px", Available: true, Unit: " px"),
            new("glow", "Ambient glow", SettingKind.Switch, Available: true),
            new("motion", "Ambient motion", SettingKind.Switch, "The glow drifts slowly while the island is open", Available: true),
        ]),
        new("Left side",
        [
            .. LeftIcons.Select(n => new SettingRow("L." + n, n, SettingKind.Switch,
                n == "Start" ? null : $"Turning it off also hides the {n} page in the island", Default: true)),
        ]),
        new("Island",
        [
            new("gili", "Show Gili", SettingKind.Switch, "Eyes in the idle island that follow the pointer", Available: true),
            new("lyrics", "Show lyrics", SettingKind.Switch,
                "Synced lyrics from lrclib.net. Only the song title, artist, album and length are sent", Default: false),
            .. Pages.Select(n => new SettingRow("T." + n, n + " page", SettingKind.Switch, Default: true)),
        ]),
        new("Right side",
        [
            .. new[] { "Weather", "Network", "CPU", "RAM", "GPU", "Disk", "Wi‑Fi", "Bluetooth", "Volume", "Brightness", "Tray icons", "Clock and date" }
                .Select(n => new SettingRow("M." + n, n, SettingKind.Switch, Default: n is not ("GPU" or "Disk"))),
            new("W.city", "Weather city", SettingKind.Segment, Options: ["Jakarta"], Default: "Jakarta"),
            new("W.bat", "Battery alert", SettingKind.Range, Unit: "%", Default: 20),
        ]),
        new("Notifications",
        [
            new("ntf", "Show Windows notifications", SettingKind.Switch,
                "New notifications from other apps also appear in the island for a few seconds. Windows asks for permission first. Tip: turn off banners for an app in Windows Settings to see it only here",
                Default: false),
            new("ntfText", "Show message text", SettingKind.Switch, "Off: only the app and the sender", Default: true),
        ]),
        new("Claude",
        [
            new("clOn", "Show Claude Code sessions", SettingKind.Switch, Default: true),
            new("limOn", "Show 5-hour usage timer", SettingKind.Switch, "Counts down to the reset and alerts you when the window ends", Default: true),
        ]),
        new("Sounds",
        [
            new("snd", "Timer sound", SettingKind.Segment, Options: ["Radar", "Chime", "Bell", "Soft", "Ping"], Default: "Chime"),
            new("alertVol", "Alert volume", SettingKind.Range, Unit: "%", Default: 80),
            new("ramp", "Gradually increase", SettingKind.Switch, Default: true),
        ]),
        new("Shortcuts", [new("keys", "Keyboard shortcuts", SettingKind.Switch, Default: true)]),
        new("About",
        [
            new("updates", "Updates", SettingKind.Switch, Default: true),
            new("logs", "Open logs folder", SettingKind.Switch, Default: true),
        ]),
    ];

    /// <summary>The current value of an available row: bool, option name, or int.</summary>
    public static object Read(AppSettings s, string key) => key switch
    {
        "h12" => !s.Use24HourClock,
        "secs" => s.ShowSeconds,
        "auto" => s.Autostart,
        "opacity" => s.BarOpacity,
        "glass" => s.Glass.ToString(),
        "barH" => s.BarHeight,
        "islH" => s.IslandHeight,
        "glow" => s.AmbientGlow,
        "motion" => s.AmbientMotion,
        "gili" => s.ShowGili,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Not an available row"),
    };

    /// <summary>Applies a row's new value; the result is normalised, so the island shrinks with a lower bar.</summary>
    public static AppSettings Write(AppSettings s, string key, object value) => (key switch
    {
        "h12" => s with { Use24HourClock = !(bool)value },
        "secs" => s with { ShowSeconds = (bool)value },
        "auto" => s with { Autostart = (bool)value },
        "opacity" => s with { BarOpacity = Number(value) },
        "glass" => s with { Glass = Enum.Parse<GlassLevel>((string)value) },
        "barH" => s with { BarHeight = Number(value) },
        "islH" => s with { IslandHeight = Number(value) },
        "glow" => s with { AmbientGlow = (bool)value },
        "motion" => s with { AmbientMotion = (bool)value },
        "gili" => s with { ShowGili = (bool)value },
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Not an available row"),
    }).Normalize();

    /// <summary>The slider's ends; the island's top end follows the bar height.</summary>
    public static (int Min, int Max) Range(AppSettings s, string key) => key switch
    {
        "opacity" => (AppSettings.MinBarOpacity, AppSettings.MaxBarOpacity),
        "barH" => (AppSettings.MinBarHeight, AppSettings.MaxBarHeight),
        "islH" => (AppSettings.MinIslandHeight, AppSettings.MaxIslandHeight(s.BarHeight)),
        "W.bat" => (5, 50),
        "alertVol" => (0, 100),
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Not a range row"),
    };

    private static int Number(object value) => (int)Math.Round(Convert.ToDouble(value, CultureInfo.InvariantCulture));
}
