using Gili.Core.Sound;

namespace Gili.Core.Tests.Sound;

public class SoundRulesTests
{
    [Fact]
    public void Muted_or_under_5_percent_warns()
    {
        Assert.True(SoundRules.WarnMuted(46, muted: true));
        Assert.True(SoundRules.WarnMuted(4, muted: false));
        Assert.False(SoundRules.WarnMuted(5, muted: false));
        Assert.Equal("Sound is off · Windows volume is muted", SoundRules.Warning);
    }

    [Fact]
    public void Card_sounds_follow_the_prd()
    {
        Assert.Equal(("Chime", true, false), SoundRules.ForCard("timer", "Chime"));
        Assert.Equal(("Soft", true, false), SoundRules.ForCard("timer", "Soft"));
        Assert.Equal(("Bell", false, false), SoundRules.ForCard("pomo-focus", "Chime"));
        Assert.Equal(("Bell", false, false), SoundRules.ForCard("pomo-break", "Chime"));
        Assert.Null(SoundRules.ForCard("conflict", "Chime"));
    }

    [Fact]
    public void Todo_reminders_ping_once() =>
        Assert.Equal(("Ping", false, false), SoundRules.ForCard("todo:3", "Chime"));

    [Fact]
    public void Alarm_cards_ring_on_repeat_with_the_ramp()
    {
        Assert.Equal(("Soft", true, true), SoundRules.ForCard("alarm:7", "Chime", alarmSound: "Soft"));
        Assert.Equal(("Radar", true, true), SoundRules.ForCard("alarm:7", "Chime"));
    }
}
