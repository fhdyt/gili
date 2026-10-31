using Gili.Core.Demo;
using Gili.Core.Devices;
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
    public void A_copy_comes_first() =>
        Assert.Equal("Meeting at 3 in the Merapi room, bring a charger", Assert.IsType<DemoCopy>(Steps[0].Action).Text);

    [Fact]
    public void A_ten_second_timer_runs_and_its_card_is_closed()
    {
        var start = Steps.Single(s => s.Action is DemoTimer);
        Assert.Equal((10, "Tea"), (((DemoTimer)start.Action).Seconds, ((DemoTimer)start.Action).Label));
        var close = Steps.Single(s => s.Action is DemoDismiss { Card: "timer" });
        Assert.True(close.At >= start.At + TimeSpan.FromSeconds(12));
    }

    [Fact]
    public void A_todo_reminder_shows_and_closes_itself()
    {
        var reminder = (DemoReminder)Steps.Single(s => s.Action is DemoReminder).Action;
        Assert.Equal(("Prepare meeting slides", new TimeOnly(15, 0)), (reminder.Title, reminder.Time));
        Assert.DoesNotContain(Steps, s => s.Action is DemoDismiss { Card: DemoReminder.CardId });
    }

    [Fact]
    public void An_alarm_card_shows_and_is_stopped()
    {
        var ring = Steps.Single(s => s.Action is DemoAlarm);
        Assert.Equal(("Take medicine", new TimeOnly(14, 30)), (((DemoAlarm)ring.Action).Label, ((DemoAlarm)ring.Action).Time));
        var stop = Steps.Single(s => s.Action is DemoDismiss { Card: DemoAlarm.CardId });
        Assert.True(stop.At >= ring.At + TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void Music_starts_after_the_copy()
    {
        var music = Steps.First(s => s.Action is DemoMedia);
        Assert.True(music.At >= Steps[0].At + TimeSpan.FromSeconds(3));
        var media = Assert.IsType<DemoMedia>(music.Action).State!;
        Assert.Equal(("Midnight City", "M83", "Spotify.exe"), (media.Title, media.Artist, media.AppId));
        Assert.True(media.IsPlaying);
        Assert.Equal(TimeSpan.FromSeconds(243), media.Duration);
        Assert.Equal(TimeSpan.FromSeconds(42), media.Position);
    }

    [Fact]
    public void Pages_open_in_order() =>
        Assert.Equal(
            [IslandPage.Media, IslandPage.Timer, IslandPage.Calendar, IslandPage.Todo, IslandPage.System],
            Steps.Select(s => s.Action).OfType<DemoOpen>().Select(o => o.Page));

    [Fact]
    public void Demo_swaps_power_for_volume()
    {
        var controls = Steps.Where(s => s.Action is DemoControl).ToList();
        Assert.Equal([ControlKind.Power, ControlKind.Volume], controls.Select(s => ((DemoControl)s.Action).Kind));
        var close = Steps.First(s => s.At > controls[1].At && s.Action is DemoClose);
        Assert.True(close.At - controls[1].At >= TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Charger_goes_in_out_then_the_battery_runs_low_and_charging_holds()
    {
        var power = Steps.Select(s => s.Action).OfType<DemoPower>().Select(p => p.Power).ToList();
        Assert.Equal(4, power.Count);
        Assert.True(power[0] is { Charging: true, PluggedIn: true, Percent: 64, HasBattery: true });
        Assert.True(power[1] is { Charging: false, PluggedIn: false });
        Assert.True(power[2] is { PluggedIn: false, Percent: 20 });
        Assert.True(power[3] is { PluggedIn: true, Charging: false, Percent: 79, OnHold: true });
    }

    [Fact]
    public void Caps_lock_and_earbuds_show_before_the_timer_card()
    {
        var card = Steps.Single(s => s.Action is DemoTimer).At + TimeSpan.FromSeconds(10);
        var caps = Steps.Single(s => s.Action is DemoLockKeys);
        Assert.Equal(new LockKeys(Caps: true, Num: false), ((DemoLockKeys)caps.Action).Keys);
        var bluetooth = Steps.Where(s => s.Action is DemoBluetooth).ToList();
        Assert.Equal(
            [new BluetoothChange("demo", "Galaxy Buds2 Pro", BluetoothKind.Audio, true, 80), new BluetoothChange("demo", "Galaxy Buds2 Pro", BluetoothKind.Audio, false, 80)],
            bluetooth.Select(s => ((DemoBluetooth)s.Action).Change));
        // Caps 1.5 s, then connected 4 s and disconnected 2 s, each after the last ends.
        Assert.True(bluetooth[0].At >= caps.At + TimeSpan.FromSeconds(1.5));
        Assert.True(bluetooth[1].At >= bluetooth[0].At + TimeSpan.FromSeconds(4));
        Assert.True(bluetooth[1].At + TimeSpan.FromSeconds(2) <= card);
    }

    [Fact]
    public void Privacy_dots_show_while_the_island_is_open()
    {
        var privacy = Steps.Where(s => s.Action is DemoPrivacy).ToList();
        Assert.Equal([new PrivacyUse(["Zoom"], ["Discord"]), PrivacyUse.None], privacy.Select(s => ((DemoPrivacy)s.Action).Use));
        Assert.True(privacy[0].At > Steps.First(s => s.Action is DemoOpen).At);
        Assert.True(privacy[1].At > Steps.Last(s => s.Action is DemoOpen).At);
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
    public void Lasts_under_a_minute() => Assert.InRange(Steps[^1].At.TotalSeconds, 25, 60);

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
