using Gili.Core.Timers;

namespace Gili.Core.Tests.Timers;

public class TimerCardRulesTests
{
    private static readonly LabeledTimer Tea = new(1, "Tea", CountdownTimer.Default.WithTotal(10));

    [Fact]
    public void A_duration_edit_dismisses_the_card_and_wins_over_a_relabel()
    {
        Assert.Equal(CardEffect.Dismiss, TimerCardRules.ForEdit(Tea, "Tea", 20));
        Assert.Equal(CardEffect.Dismiss, TimerCardRules.ForEdit(Tea, "Green tea", 20));
    }

    [Fact]
    public void A_label_only_edit_refreshes_the_card()
    {
        Assert.Equal(CardEffect.Refresh, TimerCardRules.ForEdit(Tea, "Green tea", 10));
        Assert.Equal(CardEffect.Refresh, TimerCardRules.ForEdit(Tea, "  ", 10));
    }

    [Fact]
    public void An_unchanged_edit_does_nothing()
    {
        Assert.Equal(CardEffect.None, TimerCardRules.ForEdit(Tea, "Tea", 10));
        Assert.Equal(CardEffect.None, TimerCardRules.ForEdit(Tea, " Tea ", 10));
    }

    [Fact]
    public void Starting_or_resetting_dismisses_the_card_and_pausing_does_not()
    {
        var now = new DateTimeOffset(2026, 10, 31, 0, 0, 0, TimeSpan.Zero);
        Assert.True(TimerCardRules.StartOrPauseDismisses(Tea.Clock));
        Assert.False(TimerCardRules.StartOrPauseDismisses(Tea.Clock.Start(now)));
        Assert.True(TimerCardRules.ResetDismisses);
    }
}
