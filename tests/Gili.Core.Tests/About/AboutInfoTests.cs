using Gili.Core.About;

namespace Gili.Core.Tests.About;

public class AboutInfoTests
{
    [Theory]
    [InlineData(InstallKind.Installed, "0.1.1 · GitHub")]
    [InlineData(InstallKind.Store, "0.1.1 · Microsoft Store")]
    [InlineData(InstallKind.Development, "0.1.1 · Development build")]
    public void Version_line_names_how_gili_was_installed(InstallKind kind, string expected) =>
        Assert.Equal(expected, AboutInfo.VersionLine(new Version(0, 1, 1, 0), kind));

    [Fact]
    public void Version_line_keeps_three_parts() =>
        Assert.Equal("2.0.0 · GitHub", AboutInfo.VersionLine(new Version(2, 0), InstallKind.Installed));
}

public class UninstallStepsTests
{
    [Fact]
    public void Every_step_runs_even_after_one_fails()
    {
        var ran = new List<string>();
        var failures = new List<string>();
        UninstallSteps.Run(
        [
            new("Claude hooks", () => ran.Add("hooks")),
            new("taskbar", () => throw new IOException("locked")),
            new("start entry", () => ran.Add("run")),
        ], (step, ex) => failures.Add($"{step}: {ex.Message}"));

        Assert.Equal(["hooks", "run"], ran);
        Assert.Equal(["taskbar: locked"], failures);
    }

    [Fact]
    public void Returns_whether_every_step_worked()
    {
        Assert.True(UninstallSteps.Run([new("a", () => { })], (_, _) => { }));
        Assert.False(UninstallSteps.Run([new("a", () => throw new UnauthorizedAccessException())], (_, _) => { }));
    }
}

public class AnotherCopyTests
{
    [Theory]
    [InlineData(InstallKind.Installed, true, false, true)]
    [InlineData(InstallKind.Installed, false, true, false)]
    [InlineData(InstallKind.Store, false, true, true)]
    [InlineData(InstallKind.Store, true, false, false)]
    [InlineData(InstallKind.Development, true, true, false)]
    public void The_other_release_path_counts_but_not_itself(InstallKind kind, bool storeInstalled, bool githubInstalled, bool warned) =>
        Assert.Equal(warned, AboutInfo.AnotherCopyNote(kind, storeInstalled, githubInstalled) is not null);

    [Fact]
    public void The_note_says_what_to_do() =>
        Assert.Equal("Another copy of Gili is installed. Uninstall one of them in Windows Settings › Apps",
            AboutInfo.AnotherCopyNote(InstallKind.Installed, storeInstalled: true, githubInstalled: true));

    [Theory]
    [InlineData("Gili", true)]
    [InlineData("12345Fhdyt.Gili", true)]
    [InlineData("Microsoft.Gili", true)]
    [InlineData("GiliTools", false)]
    [InlineData("Microsoft.WindowsCalculator", false)]
    public void Packages_named_gili_are_the_store_copy(string name, bool gili) =>
        Assert.Equal(gili, AboutInfo.IsGiliPackage(name));
}
