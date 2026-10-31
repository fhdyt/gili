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

    /// <summary>The low battery alert (5–50 %, also at 10 %) and the unplug reminder (50–100 %, 100 is off), F-ISL-11.</summary>
    public int BatteryAlert { get; init; } = 20;
    public int UnplugReminder { get; init; } = 80;

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

    /// <summary>The tabs the island hides: its own switches and the pages of hidden left icons (PRD 6.2).</summary>
    [JsonIgnore]
    public PageSet HiddenTabs => HiddenPages | HiddenRules.PagesOf(HiddenLeft);

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
            Glass = Enum.IsDefined(Glass) ? Glass : GlassLevel.Strong,
            AlertVolume = Math.Clamp(AlertVolume, 0, 100),
            BatteryAlert = Math.Clamp(BatteryAlert, 5, 50),
            UnplugReminder = Math.Clamp(UnplugReminder, 50, 100),
            // Media has no left icon, so freeing it always leaves the island one tab.
            HiddenPages = (HiddenPages | HiddenRules.PagesOf(HiddenLeft)) == PageSet.All ? HiddenPages & ~PageSet.Media : HiddenPages,
            TimerSound = Sound.Voices.Find(TimerSound).Name,
            ModuleOrder = string.Join(",", Order),
            PomodoroFocus = plan.FocusMinutes,
            PomodoroBreak = plan.BreakMinutes,
            PomodoroLongBreak = plan.LongBreakMinutes,
            PomodoroRounds = plan.Rounds,
        };
    }
}
