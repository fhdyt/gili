using Gili.Core.About;

namespace Gili.Core.Tests.About;

public class ReleaseInfoTests
{
    private const string Latest = """
        {
          "tag_name": "v0.2.0",
          "html_url": "https://github.com/fhdyt/gili/releases/tag/v0.2.0",
          "draft": false,
          "assets": [
            { "name": "SHA256SUMS.txt", "browser_download_url": "https://example.test/SHA256SUMS.txt" },
            { "name": "Gili-Setup-0.2.0.exe", "browser_download_url": "https://github.com/fhdyt/gili/releases/download/v0.2.0/Gili-Setup-0.2.0.exe" }
          ]
        }
        """;

    [Fact]
    public void Reads_the_version_page_and_installer()
    {
        var release = ReleaseInfo.Parse(Latest)!;
        Assert.Equal(new Version(0, 2, 0), release.Version);
        Assert.Equal("https://github.com/fhdyt/gili/releases/tag/v0.2.0", release.PageUrl);
        Assert.Equal("https://github.com/fhdyt/gili/releases/download/v0.2.0/Gili-Setup-0.2.0.exe", release.InstallerUrl);
    }

    [Fact]
    public void A_tag_without_v_works_too() =>
        Assert.Equal(new Version(1, 0, 3), ReleaseInfo.Parse("""{"tag_name":"1.0.3","html_url":"https://x.test","assets":[]}""")!.Version);

    [Fact]
    public void A_release_without_its_installer_has_no_installer_url() =>
        Assert.Null(ReleaseInfo.Parse("""{"tag_name":"v1.0.0","html_url":"https://x.test","assets":[{"name":"Gili-Setup-0.9.0.exe","browser_download_url":"https://x.test/a"}]}""")!.InstallerUrl);

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""{"message":"Not Found"}""")]
    [InlineData("""{"tag_name":"latest","html_url":"https://x.test"}""")]
    [InlineData("[]")]
    public void Anything_else_is_no_release(string json) => Assert.Null(ReleaseInfo.Parse(json));
}

public class UpdateRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void An_unsigned_copy_offers_the_download_page_instead_of_installing()
    {
        var available = new UpdateState(UpdateStatus.Available, Latest: new Version(0, 2, 0), PageUrl: "https://github.com/fhdyt/gili/releases/tag/v0.2.0", NotifyOnly: true);
        Assert.Equal("Download", UpdateRules.Button(available));
        Assert.Equal("Install update", UpdateRules.Button(available with { NotifyOnly = false }));
        Assert.Equal((null, "Update available · 0.2.0 · run the new Gili-Setup over this one"), UpdateRules.Row(available));
        var quiet = new UpdateMoment(TimeSpan.FromHours(1), false, true, null);
        Assert.False(UpdateRules.ShouldInstallNow(true, available, quiet));
        Assert.True(UpdateRules.ShouldInstallNow(true, available with { NotifyOnly = false }, quiet));
    }

    [Fact]
    public void Who_checks()
    {
        Assert.Equal(UpdateStatus.NotChecked, UpdateRules.Initial(InstallKind.Store, signed: false).Status);
        Assert.Equal(UpdateStatus.NotChecked, UpdateRules.Initial(InstallKind.Installed, signed: true).Status);
        // An unsigned GitHub copy checks too, but only says so and opens the release page (revision 15).
        Assert.Equal(new UpdateState(UpdateStatus.NotChecked, NotifyOnly: true), UpdateRules.Initial(InstallKind.Installed, signed: false));
        Assert.False(UpdateRules.Initial(InstallKind.Installed, signed: true).NotifyOnly);
        Assert.Equal(new UpdateState(UpdateStatus.Off, "Only the installed copy checks for updates"),
            UpdateRules.Initial(InstallKind.Development, signed: true));
    }

    [Fact]
    public void Due_once_a_day()
    {
        Assert.True(UpdateRules.IsDue(null, Now));
        Assert.False(UpdateRules.IsDue(Now.AddHours(-23), Now));
        Assert.True(UpdateRules.IsDue(Now.AddHours(-24), Now));
        // A clock set back counts as due rather than waiting days.
        Assert.True(UpdateRules.IsDue(Now.AddHours(2), Now));
    }

    [Theory]
    [InlineData("0.2.0", UpdateStatus.Available)]
    [InlineData("0.1.1", UpdateStatus.UpToDate)]
    [InlineData("0.1.0", UpdateStatus.UpToDate)]
    public void Only_a_newer_version_is_an_update(string latest, UpdateStatus expected) =>
        Assert.Equal(expected, UpdateRules.Checked(new Version(0, 1, 1, 0), Version.Parse(latest), "https://x.test").Status);

    [Fact]
    public void Failing_stays_quiet_for_three_days()
    {
        var upToDate = new UpdateState(UpdateStatus.UpToDate, PageUrl: "https://x.test");
        Assert.Equal(upToDate, UpdateRules.Failed(upToDate, Now.AddDays(-2), Now));
        Assert.Equal(new UpdateState(UpdateStatus.CouldNotCheck, PageUrl: "https://x.test"), UpdateRules.Failed(upToDate, Now.AddDays(-3.5), Now));
        var never = new UpdateState(UpdateStatus.NotChecked);
        Assert.Equal(never, UpdateRules.Failed(never, null, Now));
    }

    [Fact]
    public void An_update_found_before_stays_after_a_failure()
    {
        var available = UpdateRules.Checked(new Version(0, 1, 1), new Version(0, 2, 0), "https://x.test");
        Assert.Equal(available, UpdateRules.Failed(available, Now.AddDays(-5), Now));
    }

    [Fact]
    public void Row_text_for_each_state()
    {
        Assert.Equal(("Not checked yet", (string?)null), UpdateRules.Row(new UpdateState(UpdateStatus.NotChecked)));
        Assert.Equal(("Up to date", (string?)null), UpdateRules.Row(new UpdateState(UpdateStatus.UpToDate)));
        Assert.Equal(((string?)null, "Update available · 0.2.0"), UpdateRules.Row(new UpdateState(UpdateStatus.Available, Latest: new Version(0, 2, 0))));
        Assert.Equal(((string?)null, "Update available · 0.2.0"), UpdateRules.Row(new UpdateState(UpdateStatus.Installing, Latest: new Version(0, 2, 0))));
        Assert.Equal(((string?)null, "Update couldn't be verified"), UpdateRules.Row(new UpdateState(UpdateStatus.NotVerified)));
        Assert.Equal(("Couldn't check for updates", (string?)null), UpdateRules.Row(new UpdateState(UpdateStatus.CouldNotCheck)));
        Assert.Equal(((string?)null, "Only the installed copy checks for updates"),
            UpdateRules.Row(new UpdateState(UpdateStatus.Off, "Only the installed copy checks for updates")));
    }

    [Fact]
    public void The_button_shows_while_an_update_waits_or_installs()
    {
        Assert.Equal("Install update", UpdateRules.Button(new UpdateState(UpdateStatus.Available)));
        Assert.Equal("Installing…", UpdateRules.Button(new UpdateState(UpdateStatus.Installing)));
        Assert.Null(UpdateRules.Button(new UpdateState(UpdateStatus.UpToDate)));
    }
}
