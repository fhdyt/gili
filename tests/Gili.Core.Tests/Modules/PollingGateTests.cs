using Gili.Core.Modules;

namespace Gili.Core.Tests.Modules;

public class PollingGateTests
{
    [Fact]
    public void Polling_stops_when_the_screen_is_off()
    {
        var gate = new PollingGate();
        Assert.True(gate.ShouldPoll);
        gate.SetScreenOn(false);
        Assert.False(gate.ShouldPoll);
    }

    [Fact]
    public void Polling_resumes_only_when_nothing_holds_it()
    {
        var gate = new PollingGate();
        gate.SetScreenOn(false);
        gate.SetFullscreen(true);
        gate.SetSuspended(true);
        gate.SetScreenOn(true);
        gate.SetSuspended(false);
        Assert.False(gate.ShouldPoll);
        gate.SetFullscreen(false);
        Assert.True(gate.ShouldPoll);
    }

    [Fact]
    public void Changed_fires_once_per_change()
    {
        var gate = new PollingGate();
        var seen = new List<bool>();
        gate.Changed += seen.Add;
        gate.SetScreenOn(false);
        gate.SetFullscreen(true);
        gate.SetScreenOn(false);
        gate.SetFullscreen(false);
        gate.SetScreenOn(true);
        Assert.Equal([false, true], seen);
    }

    [Fact]
    public void Reason_names_what_holds_polling()
    {
        var gate = new PollingGate();
        Assert.Equal("", gate.Reason);
        gate.SetScreenOn(false);
        gate.SetFullscreen(true);
        Assert.Equal("screen off, fullscreen", gate.Reason);
    }
}
