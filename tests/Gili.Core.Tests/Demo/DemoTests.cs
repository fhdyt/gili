using Gili.Core.Demo;
using Gili.Core.Island;

namespace Gili.Core.Tests.Demo;

public class DemoTests
{
    private static readonly IReadOnlyList<DemoStep> Steps = DemoScript.Phase1(new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Steps_are_in_time_order()
    {
        for (var i = 1; i < Steps.Count; i++) Assert.True(Steps[i].At >= Steps[i - 1].At);
    }

    [Fact]
    public void Music_starts_first()
    {
        var media = Assert.IsType<DemoMedia>(Steps[0].Action).State!;
        Assert.Equal(("Midnight City", "M83", "Spotify.exe"), (media.Title, media.Artist, media.AppId));
        Assert.True(media.IsPlaying);
        Assert.Equal(TimeSpan.FromSeconds(243), media.Duration);
        Assert.Equal(TimeSpan.FromSeconds(42), media.Position);
    }

    [Fact]
    public void Pages_open_in_order() =>
        Assert.Equal(
            [IslandPage.Media, IslandPage.Timer, IslandPage.Calendar, IslandPage.Todo, IslandPage.Controls],
            Steps.Select(s => s.Action).OfType<DemoOpen>().Select(o => o.Page));

    [Fact]
    public void Charger_goes_in_then_out()
    {
        var power = Steps.Select(s => s.Action).OfType<DemoPower>().Select(p => p.Power).ToList();
        Assert.Equal(2, power.Count);
        Assert.True(power[0] is { Charging: true, PluggedIn: true, Percent: 64, HasBattery: true });
        Assert.True(power[1] is { Charging: false, PluggedIn: false });
    }

    [Fact]
    public void Ends_with_no_media_and_the_island_closed()
    {
        Assert.IsType<DemoEnd>(Steps[^1].Action);
        Assert.Null(Steps.Select(s => s.Action).OfType<DemoMedia>().Last().State);
        var lastOpen = Steps.ToList().FindLastIndex(s => s.Action is DemoOpen);
        Assert.Contains(Steps.Skip(lastOpen + 1), s => s.Action is DemoClose);
    }

    [Fact]
    public void Lasts_about_30_seconds() => Assert.InRange(Steps[^1].At.TotalSeconds, 25, 35);

    [Fact]
    public void Due_returns_each_step_once()
    {
        var player = new DemoPlayer(Steps);
        var first = player.Due(TimeSpan.FromSeconds(5));
        Assert.NotEmpty(first);
        Assert.Empty(player.Due(TimeSpan.FromSeconds(5)));
        Assert.False(player.IsDone);
        var rest = player.Due(TimeSpan.MaxValue);
        Assert.Equal(Steps.Count, first.Count + rest.Count);
        Assert.True(player.IsDone);
    }
}
