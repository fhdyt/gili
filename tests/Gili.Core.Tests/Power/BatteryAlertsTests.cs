using Gili.Core.Island;
using Gili.Core.Power;

namespace Gili.Core.Tests.Power;

public class BatteryAlertsTests
{
    private const int Low = 20;
    private const int Unplug = 80;

    private readonly BatteryAlerts _alerts = new();

    private static PowerSnapshot OnBattery(int percent) => new(HasBattery: true, PluggedIn: false, Charging: false, percent);
    private static PowerSnapshot Charging(int percent) => new(HasBattery: true, PluggedIn: true, Charging: true, percent);
    private static PowerSnapshot Held(int percent) => new(HasBattery: true, PluggedIn: true, Charging: false, percent);

    private IReadOnlyList<WideEvent> Update(PowerSnapshot power, int unplugAt = Unplug) => _alerts.Update(power, Low, unplugAt);

    private IEnumerable<string> Texts(PowerSnapshot power, int unplugAt = Unplug) => Update(power, unplugAt).Select(w => w.Text);

    [Fact]
    public void On_hold_is_plugged_in_not_charging_below_full()
    {
        Assert.True(Held(79).OnHold);
        Assert.False(Held(100).OnHold);
        Assert.False(Charging(79).OnHold);
        Assert.False(new PowerSnapshot(HasBattery: false, PluggedIn: true, Charging: false, 0).OnHold);
    }

    [Fact]
    public void Low_alert_once_per_discharge()
    {
        Assert.Empty(Update(OnBattery(30)));
        var low = Assert.Single(Update(OnBattery(20)));
        Assert.Equal("battery", low.Key);
        Assert.Equal(WideIcon.Bolt, low.Icon);
        Assert.Equal(WideTone.Warn, low.Tone);
        Assert.Equal([new WideRun("Battery "), new WideRun("20%", WideRunStyle.Strong), new WideRun(" · plug in")], low.Runs);
        Assert.Equal(TimeSpan.FromSeconds(5), low.Duration);
        Assert.Empty(Update(OnBattery(19)));
    }

    [Fact]
    public void Low_alert_names_the_percent_it_saw() => Assert.Equal(["Battery 18% · plug in"], Texts(OnBattery(18)));

    [Fact]
    public void Critical_replaces_low_below_ten()
    {
        Update(OnBattery(15));
        var critical = Assert.Single(Update(OnBattery(10)));
        Assert.Equal("Battery 10%", critical.Text);
        Assert.Equal(WideTone.Danger, critical.Tone);
        Assert.Empty(Update(OnBattery(9)));
    }

    [Fact]
    public void Start_below_both_thresholds_shows_only_critical() => Assert.Equal(["Battery 8%"], Texts(OnBattery(8)));

    [Theory]
    [InlineData(79, false)]
    [InlineData(85, true)]
    [InlineData(100, false)]
    public void Start_plugged_in_is_quiet(int percent, bool charging)
    {
        var alerts = new BatteryAlerts();
        Assert.Empty(alerts.Update(new PowerSnapshot(true, PluggedIn: true, charging, percent), Low, Unplug));
        Assert.Empty(alerts.Update(new PowerSnapshot(true, PluggedIn: true, charging, Math.Min(100, percent + 1)), Low, Unplug));
    }

    [Fact]
    public void Plugging_in_resets_low_alerts()
    {
        Update(OnBattery(20));
        Assert.Equal(["Charging · 20%"], Texts(Charging(20)));
        Assert.Equal(["On battery · 20%", "Battery 20% · plug in"], Texts(OnBattery(20)));
    }

    [Fact]
    public void Plug_in_on_hold_says_on_hold_once()
    {
        Update(OnBattery(79));
        var plug = Assert.Single(Update(Held(79)));
        Assert.Equal("charger", plug.Key);
        Assert.Equal(WideTone.Accent, plug.Tone);
        Assert.Equal([new WideRun("Charging on hold · "), new WideRun("79%", WideRunStyle.Strong)], plug.Runs);
        Assert.Equal(TimeSpan.FromSeconds(5), plug.Duration);
        Assert.Empty(Update(Held(79)));
    }

    [Fact]
    public void Charging_into_the_limit_says_on_hold()
    {
        Update(OnBattery(60));
        Assert.Equal(["Charging · 60%"], Texts(Charging(60)));
        var hold = Assert.Single(Update(Held(80)));
        Assert.Equal("battery", hold.Key);
        Assert.Equal("Charging on hold · 80%", hold.Text);
    }

    [Fact]
    public void Hold_flapping_alerts_once_per_plug()
    {
        Update(OnBattery(70));
        Update(Charging(70));
        Assert.Single(Update(Held(80)));
        Assert.Empty(Update(Charging(79)));
        Assert.Empty(Update(Held(80)));
        Assert.Empty(Update(Charging(80)));
        Assert.Empty(Update(Held(79)));

        Update(OnBattery(79));
        Assert.Equal(["Charging on hold · 79%"], Texts(Held(79)));
    }

    [Fact]
    public void Unplug_reminder_waits_until_past_the_threshold()
    {
        Update(OnBattery(70));
        Update(Charging(70));
        Assert.Empty(Update(Charging(80)));
        var reminder = Assert.Single(Update(Charging(81)));
        Assert.Equal([new WideRun("80%", WideRunStyle.Strong), new WideRun(" reached · unplug")], reminder.Runs);
        Assert.Equal(WideTone.Accent, reminder.Tone);
        Assert.Empty(Update(Charging(82)));
    }

    [Fact]
    public void Unplug_reminder_off_at_100()
    {
        Update(OnBattery(90));
        Update(Charging(90), unplugAt: 100);
        Assert.Empty(Update(Charging(99), unplugAt: 100));
    }

    [Fact]
    public void Fully_charged_once_per_plug()
    {
        Update(OnBattery(95));
        Update(Charging(95));
        Assert.Equal(["Fully charged"], Texts(new PowerSnapshot(true, true, false, 100)));
        Assert.Empty(Update(new PowerSnapshot(true, true, false, 100)));
    }

    [Fact]
    public void Plugging_in_when_full_is_only_the_charger()
    {
        Update(OnBattery(100));
        Assert.Equal(["Charging · 100%"], Texts(new PowerSnapshot(true, true, false, 100)));
        Assert.Empty(Update(new PowerSnapshot(true, true, false, 100)));
    }

    [Fact]
    public void No_battery_is_quiet()
    {
        Assert.Empty(Update(new PowerSnapshot(HasBattery: false, PluggedIn: true, Charging: false, 0)));
        Assert.Empty(Update(new PowerSnapshot(HasBattery: false, PluggedIn: false, Charging: false, 0)));
    }
}
