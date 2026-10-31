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

/// <summary>Whether a row can change now, and why not when the reason is worth a note.</summary>
public readonly record struct RowState(bool Enabled, string? Note);

/// <summary>
/// The Settings window's sections and rows as in the mockup (<c>SET</c>) and PRD 6.5, and how the rows that already
/// work map onto <see cref="AppSettings"/>. Rows of later phases show their PRD default, greyed out.
/// </summary>
public static class SettingsCatalog
{
    public const string LaterVersion = "Coming in a later version";

    private static readonly string[] LeftNames = ["Start", "Timer", "Alarms", "Todo", "Notes", "Clipboard", "Shelf", "Claude"];
    /// <summary>The Right side switches by label, with the bar's id for each module (PRD 6.4).</summary>
    private static readonly Dictionary<string, string> ModuleIds = new()
    {
        ["Weather"] = "weather",
        ["Network"] = "net",
        ["CPU"] = "cpu",
        ["RAM"] = "ram",
        ["GPU"] = "gpu",
        ["Disk"] = "disk",
        ["Wi‑Fi"] = "wifi",
        ["Bluetooth"] = "bt",
        ["Volume"] = "vol",
        ["Brightness"] = "bri",
        ["Tray icons"] = "tray",
        ["Clock and date"] = "clock",
    };

    private static readonly string[] Pages = ["Media", "Timer", "Alarms", "Calendar", "Todo", "Notes", "Clipboard", "Shelf", "Claude", "System"];

    public static IReadOnlyList<SettingsSection> Sections { get; } =
    [
        new("General",
        [
            new("pos", "Bar position", SettingKind.Segment, Available: true, Options: ["Top", "Bottom"]),
            new("hideTb", "Hide Windows taskbar", SettingKind.Switch, "Gili gives you Start, tray icons and Controls instead", Available: true),
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
            .. LeftNames.Select(n => new SettingRow("L." + n, n, SettingKind.Switch,
                n == "Start" ? null : $"Turning it off also hides the {n} page in the island", Available: true)),
        ]),
        new("Island",
        [
            new("gili", "Show Gili", SettingKind.Switch, "Eyes in the idle island that follow the pointer", Available: true),
            new("lyrics", "Show lyrics", SettingKind.Switch,
                "Synced lyrics from lrclib.net. Only the song title, artist, album and length are sent", Default: false),
            .. Pages.Select(n => new SettingRow("T." + n, n + " page", SettingKind.Switch, Available: true)),
        ]),
        new("Right side",
        [
            .. ModuleIds.Keys.Select(n => new SettingRow("M." + n, n, SettingKind.Switch, Available: true)),
            new("W.city", "Weather city", SettingKind.Segment, Options: ["Jakarta"], Default: "Jakarta"),
            new("W.bat", "Battery alert", SettingKind.Range, "Also at 10%", Available: true, Unit: "%"),
            new("W.unplug", "Unplug reminder", SettingKind.Range, "Only on laptops that keep charging past it. 100% turns it off",
                Available: true, Unit: "%"),
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
            new("clOn", "Show Claude Code sessions", SettingKind.Switch, Available: true),
            new("limOn", "Show 5-hour usage timer", SettingKind.Switch, "Counts down to the reset and alerts you when the window ends", Default: true),
        ]),
        new("Sounds",
        [
            new("snd", "Timer sound", SettingKind.Segment, Available: true, Options: ["Radar", "Chime", "Bell", "Soft", "Ping"]),
            new("alertVol", "Alert volume", SettingKind.Range, Available: true, Unit: "%"),
            new("ramp", "Gradually increase", SettingKind.Switch, Available: true),
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
        "hideTb" => s.HideTaskbar,
        "clOn" => s.ClaudeSessions,
        "pos" => s.BarPosition.ToString(),
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
        "snd" => s.TimerSound,
        "alertVol" => s.AlertVolume,
        "ramp" => s.GraduallyIncrease,
        "W.bat" => s.BatteryAlert,
        "W.unplug" => s.UnplugReminder,
        _ when key.StartsWith("L.", StringComparison.Ordinal) => !s.HiddenLeft.HasFlag(Left(key)),
        _ when key.StartsWith("T.", StringComparison.Ordinal) => !s.HiddenTabs.HasFlag(Page(key)),
        _ when key.StartsWith("M.", StringComparison.Ordinal) => !s.HiddenModules.HasFlag(Module(key)),
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Not an available row"),
    };

    /// <summary>Applies a row's new value; the result is normalised, so the island shrinks with a lower bar.</summary>
    public static AppSettings Write(AppSettings s, string key, object value) => (key switch
    {
        "hideTb" => s with { HideTaskbar = (bool)value, MovedToTop = s.MovedToTop && !(bool)value },
        "clOn" => s with { ClaudeSessions = (bool)value },
        "pos" => s with { BarPosition = Enum.Parse<BarPosition>((string)value), MovedToTop = false },
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
        "snd" => s with { TimerSound = (string)value },
        "alertVol" => s with { AlertVolume = Number(value) },
        "ramp" => s with { GraduallyIncrease = (bool)value },
        "W.bat" => s with { BatteryAlert = Number(value) },
        "W.unplug" => s with { UnplugReminder = Number(value) },
        // Turning a left icon back on brings its page back too (PRD 6.2 "Saklar kiri ikut mematikan tab pulau").
        _ when key.StartsWith("L.", StringComparison.Ordinal) => (bool)value
            ? s with { HiddenLeft = s.HiddenLeft & ~Left(key), HiddenPages = s.HiddenPages & ~HiddenRules.PageOf(Left(key)) }
            : s with { HiddenLeft = s.HiddenLeft | Left(key) },
        _ when key.StartsWith("T.", StringComparison.Ordinal) => s with
        {
            HiddenPages = (bool)value ? s.HiddenPages & ~Page(key) : s.HiddenPages | Page(key),
        },
        _ when key.StartsWith("M.", StringComparison.Ordinal) => s with
        {
            HiddenModules = (bool)value ? s.HiddenModules & ~Module(key) : s.HiddenModules | Module(key),
        },
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Not an available row"),
    }).Normalize();

    /// <summary>The slider's ends; the island's top end follows the bar height.</summary>
    public static (int Min, int Max) Range(AppSettings s, string key) => key switch
    {
        "opacity" => (AppSettings.MinBarOpacity, AppSettings.MaxBarOpacity),
        "barH" => (AppSettings.MinBarHeight, AppSettings.MaxBarHeight),
        "islH" => (AppSettings.MinIslandHeight, AppSettings.MaxIslandHeight(s.BarHeight)),
        "W.bat" => (5, 50),
        "W.unplug" => (50, 100),
        "alertVol" => (0, 100),
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Not a range row"),
    };

    /// <summary>
    /// A page whose left icon is off cannot be switched on by itself, and the last visible page (or its icon) cannot be
    /// switched off, so the island never has no tab (PRD 6.3, plan 3a).
    /// </summary>
    public static RowState State(AppSettings s, string key)
    {
        if (key == "pos" && !s.HideTaskbar)
            return new(false, s.MovedToTop ? "Moved to the top because the Windows taskbar is back" : "Hide the Windows taskbar first");
        if (key.StartsWith("T.", StringComparison.Ordinal))
        {
            var page = Page(key);
            if (HiddenRules.PagesOf(s.HiddenLeft).HasFlag(page)) return new(false, "Turned off in Left side");
            if (IsLastVisible(s, page)) return new(false, null);
        }
        if (key.StartsWith("L.", StringComparison.Ordinal) && HiddenRules.PageOf(Left(key)) is var own && own != PageSet.None && IsLastVisible(s, own))
            return new(false, null);
        return new(true, null);
    }

    private static bool IsLastVisible(AppSettings s, PageSet page) => (s.HiddenTabs | page) == PageSet.All && !s.HiddenTabs.HasFlag(page);

    /// <summary>The id of a system module's row, which Settings can move; null for every other row.</summary>
    public static string? ModuleId(string key) =>
        key.StartsWith("M.", StringComparison.Ordinal) && ModuleIds.TryGetValue(key[2..], out var id) && RightModuleRules.DefaultOrder.Contains(id)
            ? id
            : null;

    /// <summary>A section's rows as the window lists them: Right side puts the system modules in the bar's order.</summary>
    public static IReadOnlyList<SettingRow> RowsOf(SettingsSection section, AppSettings s)
    {
        if (section.Name != "Right side") return section.Rows;
        var order = s.Order;
        var modules = section.Rows.Where(r => ModuleId(r.Key) is not null).OrderBy(r => IndexIn(order, ModuleId(r.Key)!));
        return [.. modules, .. section.Rows.Where(r => ModuleId(r.Key) is null)];
    }

    private static int IndexIn(IReadOnlyList<string> order, string id)
    {
        for (var i = 0; i < order.Count; i++)
        {
            if (order[i] == id) return i;
        }
        return order.Count;
    }

    private static RightModules Module(string key) => RightModuleRules.Of(ModuleIds[key[2..]]);

    private static LeftIcons Left(string key) => Enum.Parse<LeftIcons>(key[2..]);

    private static PageSet Page(string key) => Enum.Parse<PageSet>(key[2..]);

    private static int Number(object value) => (int)Math.Round(Convert.ToDouble(value, CultureInfo.InvariantCulture));
}
