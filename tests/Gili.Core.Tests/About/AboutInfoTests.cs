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
