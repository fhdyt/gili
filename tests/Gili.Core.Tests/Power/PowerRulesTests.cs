using Gili.Core.Island;
using Gili.Core.Power;

namespace Gili.Core.Tests.Power;

public class PowerRulesTests
{
    private static PowerSnapshot OnBattery(int percent = 63) => new(HasBattery: true, PluggedIn: false, Charging: false, percent);
    private static PowerSnapshot Charging(int percent = 64) => new(HasBattery: true, PluggedIn: true, Charging: true, percent);

    [Fact]
    public void The_first_reading_shows_nothing() => Assert.Null(PowerRules.WideFor(null, Charging()));

    [Fact]
    public void Plugging_in_shows_charging_for_3_seconds()
    {
        var wide = PowerRules.WideFor(OnBattery(), Charging(64));
        Assert.Equal("charger", wide?.Key);
        Assert.Equal(WideIcon.Bolt, wide?.Icon);
        Assert.Equal(WideTone.Accent, wide?.Tone);
        Assert.Equal([new WideRun("Charging · "), new WideRun("64%", WideRunStyle.Strong)], wide?.Runs);
        Assert.Equal(TimeSpan.FromSeconds(3), wide?.Duration);
    }

    [Fact]
    public void Unplugging_shows_on_battery_for_2_seconds()
    {
        var wide = PowerRules.WideFor(Charging(), OnBattery(63));
        Assert.Equal(WideTone.Text, wide?.Tone);
        Assert.Equal("On battery · 63%", wide?.Text);
        Assert.Equal(TimeSpan.FromSeconds(2), wide?.Duration);
    }

    [Fact]
    public void A_percentage_change_alone_shows_nothing() => Assert.Null(PowerRules.WideFor(Charging(64), Charging(65)));

    [Fact]
    public void A_desktop_without_battery_shows_nothing()
    {
        var desktop = new PowerSnapshot(HasBattery: false, PluggedIn: true, Charging: false, 100);
        Assert.Null(PowerRules.WideFor(desktop with { PluggedIn = false }, desktop));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void Only_a_charging_battery_is_an_activity(bool hasBattery, bool charging, bool expected) =>
        Assert.Equal(expected, PowerRules.IsCharging(new PowerSnapshot(hasBattery, PluggedIn: true, charging, 80)));
}
