using Gili.Core.Settings;

namespace Gili.Core.Tests.Settings;

public class StartupRulesTests
{
    [Fact]
    public void Startup_blocked_by_the_user_is_shown_and_never_forced()
    {
        Assert.Equal("Turned off in Task Manager › Startup apps", StartupRules.Note(StartupState.BlockedByUser));
        Assert.False(StartupRules.CanChange(StartupState.BlockedByUser));
        Assert.False(StartupRules.CanChange(StartupState.BlockedByPolicy));
        Assert.True(StartupRules.CanChange(StartupState.On));
        Assert.True(StartupRules.CanChange(StartupState.Off));
        Assert.Null(StartupRules.Note(StartupState.On));
    }
}
