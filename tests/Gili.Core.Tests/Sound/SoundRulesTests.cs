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
}
