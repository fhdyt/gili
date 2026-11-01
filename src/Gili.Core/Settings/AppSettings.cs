using System.Text.Json.Serialization;
using Gili.Core.Timers;

namespace Gili.Core.Settings;

public enum GlassLevel { Off, Light, Strong }

/// <summary>The screen edge the bar sits on (F-BAR-10).</summary>
public enum BarPosition { Top, Bottom }

/// <summary>Everything stored in settings.json. Later phases add fields with their own defaults.</summary>
public sealed record AppSettings
{
    public const int MinBarHeight = 28;
    public const int MaxBarHeight = 44;
    public const int MinIslandHeight = 20;
    public const int MinBarOpacity = 40;
    public const int MaxBarOpacity = 100;

    /// <summary>Hide Windows taskbar (F-BAR-11); its original auto-hide state lives in taskbar-state.json, not here.</summary>
    public bool HideTaskbar { get; init; }

    /// <summary>Bottom only while the Windows taskbar is hidden, or two bars would share the edge (F-BAR-10).</summary>
    public BarPosition BarPosition { get; init; } = BarPosition.Top;

    /// <summary>The bar went back to the top because the taskbar came back; Settings says so until the user picks again.</summary>
    public bool MovedToTop { get; init; }

    /// <summary>Show Claude Code sessions (F-CLD-01): off until the user turns it on, since it edits ~/.claude/settings.json.</summary>
    public bool ClaudeSessions { get; init; }

    /// <summary>Keyboard shortcuts (F-TOOL-10): the global hotkeys, each changeable in Settings (F-TOOL-11).</summary>
    public bool KeyboardShortcuts { get; init; } = true;
    public string HotkeyIsland { get; init; } = Hotkeys.Default(HotkeyAction.Island);
    public string HotkeyClipboard { get; init; } = Hotkeys.Default(HotkeyAction.Clipboard);
    public string HotkeyNewNote { get; init; } = Hotkeys.Default(HotkeyAction.NewNote);
    public string HotkeyAddTask { get; init; } = Hotkeys.Default(HotkeyAction.AddTask);

    public string HotkeyFor(HotkeyAction action) => action switch
    {
        HotkeyAction.Island => HotkeyIsland,
        HotkeyAction.Clipboard => HotkeyClipboard,
        HotkeyAction.NewNote => HotkeyNewNote,
        _ => HotkeyAddTask,
    };

    public AppSettings WithHotkey(HotkeyAction action, string hotkey) => action switch
    {
        HotkeyAction.Island => this with { HotkeyIsland = hotkey },
        HotkeyAction.Clipboard => this with { HotkeyClipboard = hotkey },
        HotkeyAction.NewNote => this with { HotkeyNewNote = hotkey },
        _ => this with { HotkeyAddTask = hotkey },
    };

    /// <summary>Show 5-hour usage timer (F-CLD-08): on by default, but only while Claude sessions are on.</summary>
    public bool ClaudeUsageTimer { get; init; } = true;

    public bool Use24HourClock { get; init; } = true;
    public int BarOpacity { get; init; } = 100;

    /// <summary>The bar's tint as #RRGGBB (F-BAR-14): a preset from <see cref="BarColors"/> or a custom code.</summary>
    public string BarColor { get; init; } = BarColors.Default;

    /// <summary>How much of the bar's backdrop stays while the island floats, 0–100 % (F-BAR-15); 0 keeps the bar clear.</summary>
    public int OpenOpacity { get; init; }

    /// <summary>The bar looks as it does with the island open while the desktop is in front (F-BAR-16).</summary>
    public bool SameOnDesktop { get; init; } = true;
    public GlassLevel Glass { get; init; } = GlassLevel.Strong;
    public int BarHeight { get; init; } = 32;
    public int IslandHeight { get; init; } = 24;
    public bool Autostart { get; init; } = true;

    /// <summary>Gili's icon in the Windows notification area, whose menu quits Gili (F-BAR-17).</summary>
    public bool TrayIcon { get; init; } = true;
    public bool ShowSeconds { get; init; } = true;
    public bool ShowGili { get; init; } = true;

    /// <summary>Show lyrics (F-ISL-26): off until the user turns it on, since song titles go to lrclib.net.</summary>
    public bool ShowLyrics { get; init; }
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

    /// <summary>Install updates automatically (F-PUB-04, revision 14): a verified update installs while the computer is left alone.</summary>
    public bool AutoUpdate { get; init; } = true;

    /// <summary>The version that ran last, so the first start of a new one can say "Updated to Gili 0.2.0".</summary>
    public string? LastRunVersion { get; init; }

    /// <summary>The last update check that got an answer, so a restart does not ask again and PRD 11's 3 days survive one (design 5d).</summary>
    public DateTimeOffset? LastUpdateCheck { get; init; }

    /// <summary>The low battery alert (5–50 %, also at 10 %) and the unplug reminder (50–100 %, 100 is off), F-ISL-11.</summary>
    public int BatteryAlert { get; init; } = 20;
    public int UnplugReminder { get; init; } = 80;

    /// <summary>
    /// The favorite apps on the left of the bar, in the user's order (F-LEFT-08), as text so two settings with the same
    /// favorites are equal, like <see cref="ModuleOrder"/>; read and change them through <see cref="FavoriteList"/>.
    /// </summary>
    public string FavoriteApps { get; init; } = "";

    [JsonIgnore]
    public IReadOnlyList<Gili.Core.Apps.FavoriteApp> FavoriteList => Gili.Core.Apps.Favorites.Parse(FavoriteApps);

    public AppSettings WithFavorites(IReadOnlyList<Gili.Core.Apps.FavoriteApp> list) => this with { FavoriteApps = Gili.Core.Apps.Favorites.Format(list) };

    /// <summary>Left icons and island pages switched off in Settings (F-BAR-12, F-ISL-23).</summary>
    public LeftIcons HiddenLeft { get; init; } = LeftIcons.None;
    public PageSet HiddenPages { get; init; } = PageSet.None;

    /// <summary>Right modules switched off in Settings; GPU and disk start off (F-RIGHT-03, F-RIGHT-04).</summary>
    public RightModules HiddenModules { get; init; } = RightModules.Gpu | RightModules.Disk;

    /// <summary>
    /// The system modules' order as comma-separated ids, so two settings with the same order are equal; read it through
    /// <see cref="Order"/>.
    /// </summary>
    public string ModuleOrder { get; init; } = string.Join(",", RightModuleRules.DefaultOrder);

    [JsonIgnore]
    public IReadOnlyList<string> Order => RightModuleRules.Repair((ModuleOrder ?? "").Split(','));

    /// <summary>
    /// The left icons the bar leaves out: those switched off, and Claude while Show Claude Code sessions is off (PRD 6.2).
    /// </summary>
    [JsonIgnore]
    public LeftIcons ShownHiddenLeft => HiddenLeft | (ClaudeSessions ? LeftIcons.None : LeftIcons.Claude);

    /// <summary>The tabs the island hides: its own switches and the pages of hidden left icons (PRD 6.2).</summary>
    [JsonIgnore]
    public PageSet HiddenTabs => HiddenPages | HiddenRules.PagesOf(ShownHiddenLeft);

    /// <summary>The pomodoro lengths in minutes and the sessions before the long break (F-TOOL-03).</summary>
    public int PomodoroFocus { get; init; } = 25;
    public int PomodoroBreak { get; init; } = 5;
    public int PomodoroLongBreak { get; init; } = 15;
    public int PomodoroRounds { get; init; } = 4;

    [JsonIgnore]
    public PomodoroPlan Pomodoro => new(PomodoroFocus, PomodoroBreak, PomodoroLongBreak, PomodoroRounds);

    public static int MaxIslandHeight(int barHeight) => barHeight - 4;

    /// <summary>Clamps every value to the ranges in PRD 6.1 and F-BAR-08.</summary>
    public AppSettings Normalize()
    {
        var barHeight = Math.Clamp(BarHeight, MinBarHeight, MaxBarHeight);
        var plan = Pomodoro.Normalize();
        var bottom = BarPosition == BarPosition.Bottom;
        return this with
        {
            BarPosition = bottom && HideTaskbar ? BarPosition.Bottom : BarPosition.Top,
            MovedToTop = MovedToTop || (bottom && !HideTaskbar),
            BarHeight = barHeight,
            IslandHeight = Math.Clamp(IslandHeight, MinIslandHeight, MaxIslandHeight(barHeight)),
            BarOpacity = Math.Clamp(BarOpacity, MinBarOpacity, MaxBarOpacity),
            BarColor = BarColors.Parse(BarColor) ?? BarColors.Default,
            FavoriteApps = Gili.Core.Apps.Favorites.Format(Gili.Core.Apps.Favorites.Repair(FavoriteList)),
            OpenOpacity = Math.Clamp(OpenOpacity, 0, 100),
            Glass = Enum.IsDefined(Glass) ? Glass : GlassLevel.Strong,
            AlertVolume = Math.Clamp(AlertVolume, 0, 100),
            BatteryAlert = Math.Clamp(BatteryAlert, 5, 50),
            UnplugReminder = Math.Clamp(UnplugReminder, 50, 100),
            // Media has no left icon, so freeing it always leaves the island one tab.
            HiddenPages = (HiddenPages | HiddenRules.PagesOf(ShownHiddenLeft)) == PageSet.All ? HiddenPages & ~PageSet.Media : HiddenPages,
            TimerSound = Sound.Voices.Find(TimerSound).Name,
            ModuleOrder = string.Join(",", Order),
            PomodoroFocus = plan.FocusMinutes,
            PomodoroBreak = plan.BreakMinutes,
            PomodoroLongBreak = plan.LongBreakMinutes,
            PomodoroRounds = plan.Rounds,
        };
    }
}
