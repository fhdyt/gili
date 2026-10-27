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
            new HashSet<string> { "h12", "secs", "auto", "opacity", "glass", "barH", "islH", "glow", "motion", "gili", "snd", "alertVol", "ramp" },
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
            ["Weather", "Network", "CPU", "RAM", "GPU", "Disk", "Wi‑Fi", "Bluetooth", "Volume", "Brightness", "Tray icons", "Clock and date", "Weather city", "Battery alert"],
            rows.Select(r => r.Label));
        Assert.Equal(false, rows.Single(r => r.Label == "GPU").Default);
        Assert.Equal(false, rows.Single(r => r.Label == "Disk").Default);
    }

    [Fact]
    public void Left_side_help_names_the_page()
    {
        var rows = Section("Left side").Rows;
        Assert.Equal("Turning it off also hides the Timer page in the island", rows.Single(r => r.Label == "Timer").Help);
        Assert.Null(rows.Single(r => r.Label == "Start").Help);
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
        Assert.False(SettingsCatalog.Write(s, "gili", false).ShowGili);
        Assert.False(SettingsCatalog.Write(s, "glow", false).AmbientGlow);
        Assert.False(SettingsCatalog.Write(s, "motion", false).AmbientMotion);
        Assert.False(SettingsCatalog.Write(s, "auto", false).Autostart);
        Assert.Equal(70, SettingsCatalog.Write(s, "opacity", 70).BarOpacity);
    }
}
