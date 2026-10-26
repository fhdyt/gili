using Gili.Core.Diagnostics;

namespace Gili.Core.Tests.Diagnostics;

public class CrashPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_first_crash_restarts() => Assert.True(CrashPolicy.ShouldRestart(null, Now));

    [Fact]
    public void A_second_crash_within_a_minute_stays_down() =>
        Assert.False(CrashPolicy.ShouldRestart(Now.AddSeconds(-59), Now));

    [Fact]
    public void A_crash_more_than_a_minute_after_the_last_restarts_again() =>
        Assert.True(CrashPolicy.ShouldRestart(Now.AddSeconds(-61), Now));
}
