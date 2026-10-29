using Gili.Core.Settings;
using Gili.Core.Timers;
using Gili.Data;

namespace Gili.Core.Tests.Data;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gili-tests", Guid.NewGuid().ToString("N"));

    private SettingsStore Store => new(_dir);
    private string SettingsFile => Path.Combine(_dir, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Missing_file_gives_defaults()
    {
        var result = Store.Load();
        Assert.Equal(new AppSettings(), result.Settings);
        Assert.False(result.WasCorrupt);
    }

    [Fact]
    public void Saved_settings_load_back()
    {
        var saved = new AppSettings { BarHeight = 36, Glass = GlassLevel.Light, Use24HourClock = false };
        Store.Save(saved);
        Assert.Equal(saved, Store.Load().Settings);
    }

    [Fact]
    public void Unreadable_file_is_renamed_to_bak_and_defaults_are_used()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsFile, "{ not json");

        var result = Store.Load();

        Assert.True(result.WasCorrupt);
        Assert.Equal(new AppSettings(), result.Settings);
        Assert.True(File.Exists(SettingsFile + ".bak"));
        Assert.False(File.Exists(SettingsFile));
    }

    [Fact]
    public void Missing_fields_keep_their_defaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsFile, """{ "barHeight": 36 }""");

        var settings = Store.Load().Settings;

        Assert.Equal(36, settings.BarHeight);
        Assert.Equal(24, settings.IslandHeight);
        Assert.Equal(100, settings.BarOpacity);
    }

    [Fact]
    public void Out_of_range_values_are_normalized_on_load()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsFile, """{ "barHeight": 80, "islandHeight": 60 }""");

        var settings = Store.Load().Settings;

        Assert.Equal(44, settings.BarHeight);
        Assert.Equal(40, settings.IslandHeight);
    }

    [Fact]
    public void Glass_is_stored_by_name()
    {
        Store.Save(new AppSettings { Glass = GlassLevel.Light });
        Assert.Contains("\"glass\": \"light\"", File.ReadAllText(SettingsFile));
    }

    [Fact]
    public void Pomodoro_lengths_survive_a_restart()
    {
        var saved = new AppSettings { PomodoroFocus = 50, PomodoroBreak = 10, PomodoroLongBreak = 30, PomodoroRounds = 3 };
        Store.Save(saved);
        Assert.Equal(new PomodoroPlan(50, 10, 30, 3), Store.Load().Settings.Pomodoro);
    }
}
