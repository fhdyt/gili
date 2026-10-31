using Gili.Core.Mascot;

namespace Gili.Core.Tests.Mascot;

public class GiliRulesTests
{
    [Fact]
    public void Happy_beats_dragging_and_sleep() => Assert.Equal(GiliMood.Happy, GiliRules.Mood(new(true, TimeSpan.FromHours(1), true, Happy: true)));

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
        Assert.True(GiliRules.CanBlink(GiliMood.Enjoy));
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
