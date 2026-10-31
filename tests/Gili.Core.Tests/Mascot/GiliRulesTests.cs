using Gili.Core.Mascot;

namespace Gili.Core.Tests.Mascot;

public class GiliRulesTests
{
    [Fact]
    public void Startled_beats_everything() =>
        Assert.Equal(GiliMood.Surprise, GiliRules.Mood(new(true, TimeSpan.FromHours(1), true, Happy: true, Startled: true, Focused: true)));

    [Fact]
    public void Happy_beats_dragging_and_sleep() => Assert.Equal(GiliMood.Happy, GiliRules.Mood(new(true, TimeSpan.FromHours(1), true, Happy: true)));

    [Fact]
    public void Sleep_beats_focus() => Assert.Equal(GiliMood.Sleep, GiliRules.Mood(new(false, TimeSpan.FromMinutes(5), false, Focused: true)));

    [Fact]
    public void Focus_beats_music() => Assert.Equal(GiliMood.Focus, GiliRules.Mood(new(false, TimeSpan.Zero, true, Focused: true)));

    [Fact]
    public void An_alarm_while_asleep_startles() => Assert.Equal(GiliMood.Surprise, GiliRules.Mood(new(false, TimeSpan.FromHours(1), false, Startled: true)));

    [Theory]
    [InlineData("alarm:12", true)]
    [InlineData("timer", true)]
    [InlineData("timer:3", true)]
    [InlineData("pomo-focus", true)]
    [InlineData("pomo-break", true)]
    [InlineData("todo:7", true)]
    [InlineData("conflict:1", false)]
    [InlineData("timers", false)]
    [InlineData(null, false)]
    public void Alarm_timer_pomodoro_and_reminder_cards_startle(string? card, bool startles) => Assert.Equal(startles, GiliRules.Startles(card));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(60, 1)]
    [InlineData(61, 2)]
    [InlineData(1499, 25)]
    public void A_timer_shows_its_minutes_rounded_up(int seconds, int minute) => Assert.Equal(minute, GiliRules.TimerMinute(TimeSpan.FromSeconds(seconds)));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(59, 0)]
    [InlineData(60, 1)]
    [InlineData(3599, 59)]
    public void A_stopwatch_shows_its_minutes_rounded_down(int seconds, int minute) => Assert.Equal(minute, GiliRules.StopwatchMinute(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void A_new_minute_brings_a_glance_at_most_every_four_seconds()
    {
        var at = new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(GiliRules.Glances(25, 24, at.AddSeconds(-4), at));
        Assert.False(GiliRules.Glances(25, 24, at.AddSeconds(-3.9), at));
        Assert.False(GiliRules.Glances(25, 25, DateTime.MinValue, at));
        // The first minute read, or the slot leaving a timer, is no change to glance at.
        Assert.False(GiliRules.Glances(null, 25, DateTime.MinValue, at));
        Assert.False(GiliRules.Glances(25, null, DateTime.MinValue, at));
    }

    [Fact]
    public void Dragging_beats_sleep_and_music() => Assert.Equal(GiliMood.Wide, GiliRules.Mood(new(true, TimeSpan.FromHours(1), true)));

    [Fact]
    public void Sleep_beats_enjoy() => Assert.Equal(GiliMood.Sleep, GiliRules.Mood(new(false, TimeSpan.FromMinutes(5), true)));

    [Fact]
    public void Under_five_minutes_is_awake() => Assert.Equal(GiliMood.Enjoy, GiliRules.Mood(new(false, TimeSpan.FromSeconds(299), true)));

    [Fact]
    public void Nothing_going_on_is_neutral() => Assert.Equal(GiliMood.Neutral, GiliRules.Mood(new(false, TimeSpan.Zero, false)));

    [Fact]
    public void Sleeping_wide_and_happy_eyes_do_not_blink()
    {
        Assert.False(GiliRules.CanBlink(GiliMood.Sleep));
        Assert.False(GiliRules.CanBlink(GiliMood.Wide));
        Assert.False(GiliRules.CanBlink(GiliMood.Happy));
        Assert.False(GiliRules.CanBlink(GiliMood.Surprise));
        Assert.True(GiliRules.CanBlink(GiliMood.Enjoy));
        Assert.True(GiliRules.CanBlink(GiliMood.Focus));
    }

    [Fact]
    public void Island_eyes_reach_the_end_at_40_percent_of_the_screen()
    {
        Assert.Equal((1.0, 0.0), GiliRules.Aim(960 + 768, 540, 960, GiliForm.InIsland, 1920, 1080));
        Assert.Equal((-0.5, 0.0), GiliRules.Aim(960 - 384, 540, 960, GiliForm.InIsland, 1920, 1080));
    }

    [Fact]
    public void Circle_eyes_lean_fully_at_400_px() => Assert.Equal(1, GiliRules.Aim(500, 540, 100, GiliForm.Circle, 1920, 1080).X);

    [Fact]
    public void Pointer_at_the_top_looks_up() => Assert.Equal(-1, GiliRules.Aim(960, 0, 960, GiliForm.InIsland, 1920, 1080).Y);

    [Fact]
    public void Island_eyes_travel_to_9_px_from_the_edge_and_3_px_from_top_and_bottom()
    {
        var (x, y) = GiliRules.Offset(1, 1, GiliForm.InIsland, islandWidth: 200, islandHeight: 24, eyesWidth: 22.16, eyesHeight: 10.08);
        Assert.Equal(79.92, x, 2);
        Assert.Equal(3.96, y, 2);
    }

    [Fact]
    public void Circle_and_menu_eyes_lean_2_and_3_px()
    {
        Assert.Equal((2.0, -2.0), GiliRules.Offset(1, -1, GiliForm.Circle, 0, 0, 0, 0));
        Assert.Equal((3.0, 3.0), GiliRules.Offset(1, 1, GiliForm.Menu, 0, 0, 0, 0));
    }

    [Fact]
    public void Island_eyes_are_42_percent_of_the_island() => Assert.Equal(new GiliEyeSize(10.08, 3, 2), GiliRules.Eyes(GiliForm.InIsland, 24));

    [Fact]
    public void Blinks_come_every_3_to_6_seconds_one_in_five_double()
    {
        var random = new Random(7);
        var plans = Enumerable.Range(0, 10_000).Select(_ => GiliRules.NextBlink(random)).ToList();
        Assert.All(plans, p => Assert.InRange(p.Wait.TotalMilliseconds, 3000, 6350));
        Assert.InRange(plans.Count(p => p.Double) / 10_000.0, 0.18, 0.22);
    }
}
