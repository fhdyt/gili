namespace Gili.Core.Tests;

public class LaunchRulesTests
{
    [Fact]
    public void Opening_gili_again_by_hand_shows_settings_in_the_running_copy() =>
        Assert.True(LaunchRules.SignalsRunningCopy([], startedByStartupTask: false));

    [Fact]
    public void A_second_copy_started_at_login_leaves_quietly()
    {
        Assert.False(LaunchRules.SignalsRunningCopy(["--autostart"], startedByStartupTask: false));
        Assert.False(LaunchRules.SignalsRunningCopy([], startedByStartupTask: true));
    }

    [Fact]
    public void The_run_entry_marks_its_launch_as_autostart() =>
        Assert.Equal(@"""C:\Gili\Gili.exe"" --autostart", LaunchRules.RunCommand(@"C:\Gili\Gili.exe"));
}
