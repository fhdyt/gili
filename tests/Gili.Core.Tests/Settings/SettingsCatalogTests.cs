using Gili.Core.Settings;

namespace Gili.Core.Tests.Settings;

public class SettingsCatalogTests
{
    private static IEnumerable<SettingRow> AllRows => SettingsCatalog.Sections.SelectMany(s => s.Rows);

    private static SettingsSection Section(string name) => SettingsCatalog.Sections.Single(s => s.Name == name);

    [Fact]
    public void Sections_follow_the_mockup_then_the_prd() =>
        Assert.Equal(
            ["General", "Appearance", "Left side", "Island", "Right side", "Notifications", "Claude", "Sounds", "Shortcuts", "About"],
            SettingsCatalog.Sections.Select(s => s.Name));

    [Fact]
    public void Phase_1_rows_are_available() =>
        Assert.Equal(
            new HashSet<string> { "version", "updates", "notes", "logs", "prepare", "clOn", "limOn", "keys", "W.city", "hk.island", "hk.clipboard", "hk.note", "hk.task", "pos", "hideTb", "h12", "secs", "auto", "tray", "barColor", "opacity", "openOp", "desktop", "glass", "barH", "islH", "glow", "motion", "gili", "snd", "alertVol", "ramp", "W.bat", "W.unplug",
                "L.Start", "L.Timer", "L.Alarms", "L.Todo", "L.Notes", "L.Clipboard", "L.Shelf", "L.Claude", "L.Favorites", "favs", "L.Apps",
                "T.Media", "T.Timer", "T.Alarms", "T.Calendar", "T.Todo", "T.Notes", "T.Clipboard", "T.Shelf", "T.Claude", "T.System",
                "M.Weather", "M.Network", "M.CPU", "M.RAM", "M.GPU", "M.Disk", "M.Wi‑Fi", "M.Bluetooth", "M.Volume", "M.Brightness", "M.Tray icons", "M.Clock and date", "M.Power" },
            AllRows.Where(r => r.Available).Select(r => r.Key).ToHashSet());

    [Fact]
    public void Later_rows_say_coming_in_a_later_version()
    {
        Assert.Equal("Coming in a later version", SettingsCatalog.LaterVersion);
        Assert.All(AllRows.Where(r => !r.Available), r => Assert.NotNull(r.Default));
    }

    [Fact]
    public void Cpu_temperature_is_gone() => Assert.DoesNotContain(AllRows, r => r.Label == "CPU temperature");

    [Fact]
    public void Right_side_rows_match_the_prd()
    {
        var rows = Section("Right side").Rows;
        Assert.Equal(
            ["Weather", "Network", "CPU", "RAM", "GPU", "Disk", "Wi‑Fi", "Bluetooth", "Volume", "Brightness", "Tray icons", "Clock and date", "Power", "Weather city", "Battery alert", "Unplug reminder"],
            rows.Select(r => r.Label));
        Assert.Equal(false, SettingsCatalog.Read(new AppSettings(), "M.GPU"));
        Assert.Equal(false, SettingsCatalog.Read(new AppSettings(), "M.Disk"));
    }

    [Fact]
    public void Left_side_help_names_the_page()
    {
        var rows = Section("Left side").Rows;
        Assert.Equal("Turning it off also hides the Timer page in the island", rows.Single(r => r.Label == "Timer").Help);
        Assert.Null(rows.Single(r => r.Label == "Start").Help);
    }

    [Fact]
    public void Running_apps_can_be_hidden_without_touching_a_tab()
    {
        var rows = Section("Left side").Rows;
        Assert.Equal("L.Apps", rows[^1].Key);
        Assert.Equal("Running apps", rows[^1].Label);
        Assert.Equal("The apps that are open, after the divider", rows[^1].Help);
        var s = SettingsCatalog.Write(new AppSettings(), "L.Apps", false);
        Assert.Equal(LeftIcons.Apps, s.HiddenLeft);
        Assert.Equal(PageSet.None, s.HiddenPages);
        Assert.Equal(false, SettingsCatalog.Read(s, "L.Apps"));
        Assert.True(SettingsCatalog.State(s, "L.Apps").Enabled);
        Assert.Equal(LeftIcons.None, SettingsCatalog.Write(s, "L.Apps", true).HiddenLeft);
    }

    [Fact]
    public void Twelve_hour_clock_is_the_inverse_of_24_hour()
    {
        var s = new AppSettings();
        Assert.Equal(false, SettingsCatalog.Read(s, "h12"));
        Assert.False(SettingsCatalog.Write(s, "h12", true).Use24HourClock);
    }

    [Fact]
    public void Glass_reads_and_writes_by_name()
    {
        var s = new AppSettings();
        Assert.Equal("Strong", SettingsCatalog.Read(s, "glass"));
        Assert.Equal(GlassLevel.Light, SettingsCatalog.Write(s, "glass", "Light").Glass);
    }

    [Fact]
    public void Island_height_range_ends_at_bar_height_minus_4()
    {
        Assert.Equal((20, 26), SettingsCatalog.Range(new AppSettings { BarHeight = 30 }, "islH"));
        Assert.Equal(24, SettingsCatalog.Write(new AppSettings { IslandHeight = 26, BarHeight = 32 }, "barH", 28).IslandHeight);
    }

    [Fact]
    public void Ranges_match_the_prd()
    {
        var s = new AppSettings();
        Assert.Equal((40, 100), SettingsCatalog.Range(s, "opacity"));
        Assert.Equal("%", AllRows.Single(r => r.Key == "opacity").Unit);
        Assert.Equal((28, 44), SettingsCatalog.Range(s, "barH"));
        Assert.Equal(" px", AllRows.Single(r => r.Key == "barH").Unit);
    }

    [Fact]
    public void Sound_settings_read_and_write()
    {
        var s = new AppSettings();
        Assert.Equal(("Chime", 80, true), (SettingsCatalog.Read(s, "snd"), SettingsCatalog.Read(s, "alertVol"), SettingsCatalog.Read(s, "ramp")));
        Assert.Equal("Bell", SettingsCatalog.Write(s, "snd", "Bell").TimerSound);
        Assert.Equal(55, SettingsCatalog.Write(s, "alertVol", 55).AlertVolume);
        Assert.False(SettingsCatalog.Write(s, "ramp", false).GraduallyIncrease);
        Assert.Equal((0, 100), SettingsCatalog.Range(s, "alertVol"));
    }

    [Fact]
    public void Switches_write_their_settings()
    {
        var s = new AppSettings();
        Assert.False(SettingsCatalog.Write(s, "secs", false).ShowSeconds);
        Assert.True(SettingsCatalog.Write(s, "hideTb", true).HideTaskbar);
        Assert.True(SettingsCatalog.Write(s, "clOn", true).ClaudeSessions);
        Assert.False(new AppSettings().ClaudeSessions);
        Assert.False((bool)SettingsCatalog.Read(s, "hideTb"));
        Assert.False(SettingsCatalog.Write(s, "gili", false).ShowGili);
        Assert.False(SettingsCatalog.Write(s, "glow", false).AmbientGlow);
        Assert.False(SettingsCatalog.Write(s, "motion", false).AmbientMotion);
        Assert.False(SettingsCatalog.Write(s, "auto", false).Autostart);
        Assert.True(s.TrayIcon);
        Assert.False(SettingsCatalog.Write(s, "tray", false).TrayIcon);
        Assert.Equal(70, SettingsCatalog.Write(s, "opacity", 70).BarOpacity);
    }

    [Fact]
    public void Battery_alert_reads_writes_and_says_ten_too()
    {
        var row = AllRows.Single(r => r.Key == "W.bat");
        Assert.Equal("Also at 10%", row.Help);
        Assert.Equal(20, SettingsCatalog.Read(new AppSettings(), "W.bat"));
        Assert.Equal(15, SettingsCatalog.Write(new AppSettings(), "W.bat", 15).BatteryAlert);
        Assert.Equal((5, 50), SettingsCatalog.Range(new AppSettings(), "W.bat"));
    }

    [Fact]
    public void Unplug_reminder_row_reads_and_writes()
    {
        var row = AllRows.Single(r => r.Key == "W.unplug");
        Assert.Equal(("Unplug reminder", SettingKind.Range, "%"), (row.Label, row.Kind, row.Unit));
        Assert.Equal("Only on laptops that keep charging past it. 100% turns it off", row.Help);
        Assert.Equal(80, SettingsCatalog.Read(new AppSettings(), "W.unplug"));
        Assert.Equal(90, SettingsCatalog.Write(new AppSettings(), "W.unplug", 90).UnplugReminder);
        Assert.Equal((50, 100), SettingsCatalog.Range(new AppSettings(), "W.unplug"));
    }

    [Fact]
    public void Left_icon_off_hides_its_page_and_locks_the_switch()
    {
        var s = SettingsCatalog.Write(new AppSettings(), "L.Timer", false);
        Assert.Equal(LeftIcons.Timer, s.HiddenLeft);
        Assert.Equal(false, SettingsCatalog.Read(s, "L.Timer"));
        Assert.Equal(false, SettingsCatalog.Read(s, "T.Timer"));
        Assert.True(s.HiddenTabs.HasFlag(PageSet.Timer));
        Assert.Equal(new RowState(false, "Turned off in Left side"), SettingsCatalog.State(s, "T.Timer"));
        Assert.Equal(new RowState(true, null), SettingsCatalog.State(s, "T.Alarms"));
    }

    [Fact]
    public void Left_icon_on_brings_its_page_back()
    {
        var hidden = new AppSettings { HiddenLeft = LeftIcons.Timer, HiddenPages = PageSet.Timer };
        var s = SettingsCatalog.Write(hidden, "L.Timer", true);
        Assert.Equal((LeftIcons.None, PageSet.None), (s.HiddenLeft, s.HiddenPages));
    }

    [Fact]
    public void Island_page_switches_write_hidden_pages()
    {
        var s = SettingsCatalog.Write(new AppSettings(), "T.Calendar", false);
        Assert.Equal(PageSet.Calendar, s.HiddenPages);
        Assert.Equal(true, SettingsCatalog.Read(SettingsCatalog.Write(s, "T.Calendar", true), "T.Calendar"));
    }

    [Fact]
    public void Last_visible_page_cannot_be_turned_off()
    {
        var s = new AppSettings { HiddenPages = PageSet.All & ~PageSet.Timer };
        Assert.Equal(new RowState(false, null), SettingsCatalog.State(s, "T.Timer"));
        Assert.Equal(new RowState(false, null), SettingsCatalog.State(s, "L.Timer"));
        Assert.Equal(new RowState(true, null), SettingsCatalog.State(s, "T.Media"));
    }

    [Fact]
    public void Start_switch_has_no_page()
    {
        var s = SettingsCatalog.Write(new AppSettings { ClaudeSessions = true }, "L.Start", false);
        Assert.Equal((LeftIcons.Start, PageSet.None), (s.HiddenLeft, s.HiddenTabs));
        Assert.Equal(new RowState(true, null), SettingsCatalog.State(new AppSettings { HiddenPages = PageSet.All & ~PageSet.Media }, "L.Start"));
    }

    [Fact]
    public void Other_rows_are_enabled() => Assert.Equal(new RowState(true, null), SettingsCatalog.State(new AppSettings(), "glow"));
}

public class SettingsCatalogModuleTests
{
    private static SettingsSection RightSide => SettingsCatalog.Sections.Single(s => s.Name == "Right side");

    [Fact]
    public void Module_switch_reads_and_writes()
    {
        var s = new AppSettings();
        Assert.Equal(false, SettingsCatalog.Read(s, "M.GPU"));
        Assert.Equal(true, SettingsCatalog.Read(s, "M.Wi‑Fi"));
        s = SettingsCatalog.Write(s, "M.GPU", true);
        Assert.Equal(RightModules.Disk, s.HiddenModules);
        s = SettingsCatalog.Write(s, "M.Clock and date", false);
        s = SettingsCatalog.Write(s, "M.Tray icons", false);
        Assert.Equal(RightModules.Disk | RightModules.Clock | RightModules.Tray, s.HiddenModules);
    }

    [Fact]
    public void Power_can_be_hidden_but_starts_on()
    {
        var s = new AppSettings();
        Assert.Equal(true, SettingsCatalog.Read(s, "M.Power"));
        s = SettingsCatalog.Write(s, "M.Power", false);
        Assert.True(s.HiddenModules.HasFlag(RightModules.Power));
        Assert.Equal("Sleep, restart and shut down stay in the Start menu", RightSide.Rows.Single(r => r.Key == "M.Power").Help);
        Assert.True(SettingsCatalog.State(s, "M.Power").Enabled);
    }

    [Fact]
    public void Module_rows_and_the_city_are_available()
    {
        Assert.All(RightSide.Rows.Where(r => r.Key.StartsWith("M.", StringComparison.Ordinal)), r => Assert.True(r.Available));
        Assert.Equal(SettingKind.City, RightSide.Rows.Single(r => r.Key == "W.city").Kind);
        Assert.Equal("Jakarta", SettingsCatalog.Read(new AppSettings(), "W.city"));
    }

    [Fact]
    public void Right_side_rows_follow_order()
    {
        var s = new AppSettings { ModuleOrder = "disk,cpu,weather,net,ram,gpu" };
        Assert.Equal(
            ["M.Disk", "M.CPU", "M.Weather", "M.Network", "M.RAM", "M.GPU", "M.Wi‑Fi"],
            SettingsCatalog.RowsOf(RightSide, s).Take(7).Select(r => r.Key));
        Assert.Equal(RightSide.Rows.Count, SettingsCatalog.RowsOf(RightSide, s).Count);
    }

    [Fact]
    public void Other_sections_keep_their_rows()
    {
        var general = SettingsCatalog.Sections[0];
        Assert.Same(general.Rows, SettingsCatalog.RowsOf(general, new AppSettings()));
    }

    [Fact]
    public void Module_id_of_rows()
    {
        Assert.Equal("net", SettingsCatalog.ModuleId("M.Network"));
        Assert.Equal("disk", SettingsCatalog.ModuleId("M.Disk"));
        Assert.Null(SettingsCatalog.ModuleId("M.Wi‑Fi"));
        Assert.Null(SettingsCatalog.ModuleId("h12"));
    }

    [Fact]
    public void Claude_icon_and_page_wait_for_the_integration()
    {
        var off = new AppSettings();
        Assert.True(off.HiddenTabs.HasFlag(PageSet.Claude));
        Assert.True(off.ShownHiddenLeft.HasFlag(LeftIcons.Claude));
        Assert.Equal(new RowState(false, SettingsCatalog.ClaudeOff), SettingsCatalog.State(off, "L.Claude"));
        Assert.Equal(new RowState(false, SettingsCatalog.ClaudeOff), SettingsCatalog.State(off, "T.Claude"));
        var on = off with { ClaudeSessions = true };
        Assert.False(on.HiddenTabs.HasFlag(PageSet.Claude));
        Assert.True(SettingsCatalog.State(on, "L.Claude").Enabled);
    }

    [Fact]
    public void The_usage_timer_is_on_by_default_and_waits_for_the_integration()
    {
        var off = new AppSettings();
        Assert.True((bool)SettingsCatalog.Read(off, "limOn"));
        Assert.Equal(new RowState(false, SettingsCatalog.ClaudeOff), SettingsCatalog.State(off, "limOn"));
        var on = off with { ClaudeSessions = true };
        Assert.True(SettingsCatalog.State(on, "limOn").Enabled);
        Assert.False(SettingsCatalog.Write(on, "limOn", false).ClaudeUsageTimer);
    }
}

public class SettingsCatalogAboutTests
{
    private static SettingsSection About => SettingsCatalog.Sections.Single(s => s.Name == "About");

    [Fact]
    public void About_rows_follow_the_prd() =>
        Assert.Equal(["Version", "Updates", "Privacy policy", "Release notes", "Open logs folder", "Prepare to uninstall"],
            About.Rows.Select(r => r.Label));

    [Fact]
    public void Only_the_privacy_policy_waits_for_its_page() =>
        Assert.Equal(["privacy"], About.Rows.Where(r => !r.Available).Select(r => r.Key));

    [Fact]
    public void About_rows_are_text_or_buttons()
    {
        Assert.Equal(SettingKind.Info, About.Rows.Single(r => r.Key == "version").Kind);
        Assert.All(About.Rows.Where(r => r.Kind == SettingKind.Action), r => Assert.Single(r.Options!));
        Assert.Equal("Prepare", About.Rows.Single(r => r.Key == "prepare").Options![0]);
    }

    [Theory]
    [InlineData(InstallKind.Store, true)]
    [InlineData(InstallKind.Installed, false)]
    [InlineData(InstallKind.Development, false)]
    public void Only_the_store_copy_prepares_to_uninstall_from_settings(InstallKind kind, bool shown) =>
        Assert.Equal(shown, SettingsCatalog.RowsOf(About, new AppSettings(), kind).Any(r => r.Key == "prepare"));
}
