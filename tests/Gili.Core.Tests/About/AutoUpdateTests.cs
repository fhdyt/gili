using Gili.Core.About;
using Gili.Core.Island;
using Gili.Core.Settings;

namespace Gili.Core.Tests.About;

public class AutoUpdateTests
{
    private static readonly UpdateMoment Quiet = new(Idle: TimeSpan.FromMinutes(12), Fullscreen: false, IslandCompact: true, NextAlert: null);

    [Fact]
    public void Installs_once_the_computer_has_been_left_alone_for_ten_minutes()
    {
        Assert.True(UpdateRules.ShouldInstallNow(true, UpdateStatus.Available, Quiet));
        Assert.False(UpdateRules.ShouldInstallNow(true, UpdateStatus.Available, Quiet with { Idle = TimeSpan.FromMinutes(9) }));
    }

    [Fact]
    public void Waits_while_anything_is_on_screen_or_due_soon()
    {
        Assert.False(UpdateRules.ShouldInstallNow(true, UpdateStatus.Available, Quiet with { Fullscreen = true }));
        Assert.False(UpdateRules.ShouldInstallNow(true, UpdateStatus.Available, Quiet with { IslandCompact = false }));
        Assert.False(UpdateRules.ShouldInstallNow(true, UpdateStatus.Available, Quiet with { NextAlert = TimeSpan.FromMinutes(14) }));
        Assert.True(UpdateRules.ShouldInstallNow(true, UpdateStatus.Available, Quiet with { NextAlert = TimeSpan.FromMinutes(16) }));
    }

    [Fact]
    public void Never_without_the_switch_or_without_an_update()
    {
        Assert.False(UpdateRules.ShouldInstallNow(false, UpdateStatus.Available, Quiet));
        Assert.False(UpdateRules.ShouldInstallNow(true, UpdateStatus.UpToDate, Quiet));
        Assert.False(UpdateRules.ShouldInstallNow(true, UpdateStatus.Installing, Quiet));
        Assert.False(UpdateRules.ShouldInstallNow(true, UpdateStatus.NotVerified, Quiet));
    }

    [Fact]
    public void A_newer_version_than_the_last_run_says_it_was_updated()
    {
        Assert.Equal(new Version(0, 2, 0), UpdateRules.JustUpdated("0.1.1", new Version(0, 2, 0, 0)));
        Assert.Null(UpdateRules.JustUpdated("0.2.0", new Version(0, 2, 0, 0)));
        // The first run ever, or a broken value, says nothing.
        Assert.Null(UpdateRules.JustUpdated(null, new Version(0, 2, 0, 0)));
        Assert.Null(UpdateRules.JustUpdated("soon", new Version(0, 2, 0, 0)));
    }

    [Fact]
    public void The_updated_notice_names_the_version()
    {
        var wide = UpdateRules.UpdatedWide(new Version(0, 2, 0));
        Assert.Equal(WideIcon.Restart, wide.Icon);
        Assert.Equal("Updated to Gili 0.2.0", string.Concat(wide.Runs.Select(r => (r.SpaceBefore ? " " : "") + r.Text)));
        Assert.Equal(TimeSpan.FromSeconds(4), wide.Duration);
    }

    [Fact]
    public void The_switch_starts_on_and_is_an_about_row()
    {
        Assert.True(new AppSettings().AutoUpdate);
        Assert.False(SettingsCatalog.Write(new AppSettings(), "autoUpd", false).AutoUpdate);
        Assert.Equal(true, SettingsCatalog.Read(new AppSettings(), "autoUpd"));
        Assert.Contains(SettingsCatalog.Sections.Single(s => s.Name == "About").Rows, r => r.Key == "autoUpd");
    }
}
