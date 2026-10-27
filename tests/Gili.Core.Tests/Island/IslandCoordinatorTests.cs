using Gili.Core.Island;

namespace Gili.Core.Tests.Island;

public class IslandCoordinatorTests
{
    private readonly IslandCoordinator _island;
    private DateTimeOffset _now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    public IslandCoordinatorTests() => _island = new IslandCoordinator(() => _now);

    private void Advance(double seconds)
    {
        _now = _now.AddSeconds(seconds);
        _island.Tick();
    }

    private static WideEvent Wide(string key, double seconds = 3) =>
        new(key, WideIcon.None, key, null, TimeSpan.FromSeconds(seconds));

    private static IslandCard Urgent(string id) => new(id, CardPriority.Urgent, TimeSpan.Zero);

    private static IslandCard Reminder(string id, double seconds = 10) =>
        new(id, CardPriority.Reminder, TimeSpan.FromSeconds(seconds));

    [Fact]
    public void An_idle_island_is_compact_with_the_clock()
    {
        Assert.Equal(IslandMode.Compact, _island.View.Mode);
        Assert.Null(_island.View.Slot);
        Assert.Null(_island.View.Bubble);
    }

    [Fact]
    public void The_best_activity_takes_the_slot_and_the_next_one_the_bubble()
    {
        _island.SetActivity(ActivityKind.Charging, true);
        _island.SetActivity(ActivityKind.Media, true);
        Assert.Equal(ActivityKind.Media, _island.View.Slot);
        Assert.Equal(ActivityKind.Charging, _island.View.Bubble);

        _island.SetActivity(ActivityKind.Timer, true);
        Assert.Equal(ActivityKind.Timer, _island.View.Slot);
        Assert.Equal(ActivityKind.Media, _island.View.Bubble);
    }

    [Fact]
    public void The_clock_returns_when_the_last_activity_ends()
    {
        _island.SetActivity(ActivityKind.Media, true);
        _island.SetActivity(ActivityKind.Media, false);
        Assert.Null(_island.View.Slot);
    }

    [Fact]
    public void A_wide_notice_shows_for_its_duration()
    {
        _island.ShowWide(Wide("charger", 3));
        Assert.Equal(IslandMode.Wide, _island.View.Mode);
        Assert.Equal("charger", _island.View.Wide?.Key);

        Advance(2.9);
        Assert.Equal(IslandMode.Wide, _island.View.Mode);
        Advance(0.1);
        Assert.Equal(IslandMode.Compact, _island.View.Mode);
        Assert.Null(_island.View.Wide);
    }

    [Fact]
    public void Wide_notices_queue_and_a_queued_one_with_the_same_key_is_replaced()
    {
        _island.ShowWide(Wide("caps", 1.5));
        _island.ShowWide(Wide("charger", 3));
        _island.ShowWide(Wide("charger", 2) with { Text = "On battery" });

        Advance(1.5);
        Assert.Equal("On battery", _island.View.Wide?.Text);
        Advance(2);
        Assert.Equal(IslandMode.Compact, _island.View.Mode);
    }

    [Fact]
    public void A_notice_with_the_key_on_screen_replaces_it_and_restarts_its_time()
    {
        _island.ShowWide(Wide("caps", 1.5));
        Advance(1);
        _island.ShowWide(Wide("caps", 1.5) with { Text = "Caps Lock off" });
        Advance(1);
        Assert.Equal("Caps Lock off", _island.View.Wide?.Text);
    }

    [Fact]
    public void The_bubble_only_shows_while_compact()
    {
        _island.SetActivity(ActivityKind.Media, true);
        _island.SetActivity(ActivityKind.Charging, true);
        _island.ShowWide(Wide("caps"));
        Assert.Equal(IslandMode.Wide, _island.View.Mode);
        Assert.Null(_island.View.Bubble);
    }

    [Fact]
    public void An_open_page_holds_wide_notices_back_until_it_closes()
    {
        _island.Open(IslandPage.Media);
        _island.ShowWide(Wide("caps", 1.5));
        Advance(5);
        Assert.Equal(IslandMode.Expanded, _island.View.Mode);

        _island.Close();
        Assert.Equal(IslandMode.Wide, _island.View.Mode);
        Advance(1.5);
        Assert.Equal(IslandMode.Compact, _island.View.Mode);
    }

    [Fact]
    public void An_urgent_card_replaces_the_open_page_which_returns_after_it_is_answered()
    {
        _island.Open(IslandPage.Media);
        _island.ShowCard(Urgent("alarm"));
        Assert.Equal(IslandMode.Card, _island.View.Mode);
        Assert.Equal("alarm", _island.View.Card?.Id);

        Advance(600);
        Assert.Equal(IslandMode.Card, _island.View.Mode);

        _island.DismissCard("alarm");
        Assert.Equal(IslandMode.Expanded, _island.View.Mode);
        Assert.Equal(IslandPage.Media, _island.View.Page);
    }

    [Fact]
    public void A_reminder_card_closes_itself_after_its_timeout()
    {
        _island.ShowCard(Reminder("todo", 10));
        Advance(9.9);
        Assert.Equal(IslandMode.Card, _island.View.Mode);
        Advance(0.1);
        Assert.Equal(IslandMode.Compact, _island.View.Mode);
    }

    [Fact]
    public void A_reminder_waits_behind_an_urgent_card_and_then_gets_its_full_time()
    {
        _island.ShowCard(Urgent("alarm"));
        _island.ShowCard(Reminder("todo", 10));
        Assert.Equal("alarm", _island.View.Card?.Id);

        Advance(30);
        _island.DismissCard("alarm");
        Assert.Equal("todo", _island.View.Card?.Id);
        Advance(9.9);
        Assert.Equal("todo", _island.View.Card?.Id);
        Advance(0.1);
        Assert.Equal(IslandMode.Compact, _island.View.Mode);
    }

    [Fact]
    public void Showing_the_same_card_again_restarts_its_timeout()
    {
        _island.ShowCard(Reminder("claude", 8));
        Advance(6);
        _island.ShowCard(Reminder("claude", 8));
        Advance(6);
        Assert.Equal(IslandMode.Card, _island.View.Mode);
    }

    [Fact]
    public void The_drop_zone_sits_above_the_open_page_and_below_cards()
    {
        _island.Open(IslandPage.Media);
        _island.SetDropZone(true);
        Assert.Equal(IslandMode.DropZone, _island.View.Mode);

        _island.ShowCard(Urgent("alarm"));
        Assert.Equal(IslandMode.Card, _island.View.Mode);

        _island.DismissCard("alarm");
        _island.SetDropZone(false);
        Assert.Equal(IslandMode.Expanded, _island.View.Mode);
    }

    [Fact]
    public void Fullscreen_hides_the_island_and_closes_the_open_page()
    {
        _island.Open(IslandPage.Media);
        _island.SetFullscreen(true);
        Assert.Equal(IslandMode.Hidden, _island.View.Mode);

        _island.SetFullscreen(false);
        Assert.Equal(IslandMode.Compact, _island.View.Mode);
    }

    [Fact]
    public void In_fullscreen_only_urgent_cards_show_and_everything_else_is_dropped()
    {
        _island.ShowWide(Wide("caps"));
        _island.SetFullscreen(true);
        _island.ShowWide(Wide("charger"));
        _island.ShowCard(Reminder("todo"));
        Assert.Equal(IslandMode.Hidden, _island.View.Mode);

        _island.ShowCard(Urgent("alarm"));
        Assert.Equal(IslandMode.Card, _island.View.Mode);

        _island.DismissCard("alarm");
        _island.SetFullscreen(false);
        Assert.Equal(IslandMode.Compact, _island.View.Mode);
    }

    [Fact]
    public void Toggle_opens_switches_and_closes()
    {
        _island.Toggle(IslandPage.Timer);
        Assert.Equal((IslandMode.Expanded, IslandPage.Timer), (_island.View.Mode, _island.View.Page));
        _island.Toggle(IslandPage.Notes);
        Assert.Equal(IslandPage.Notes, _island.View.Page);
        _island.Toggle(IslandPage.Notes);
        Assert.Equal(IslandMode.Compact, _island.View.Mode);
    }

    [Fact]
    public void A_control_island_is_not_the_last_page_the_island_reopens()
    {
        _island.Open(IslandPage.Calendar);
        _island.Toggle(IslandPage.Controls);
        Assert.Equal(IslandPage.Controls, _island.View.Page);
        _island.Close();

        _island.OpenFor(_island.View.Slot);
        Assert.Equal(IslandPage.Calendar, _island.View.Page);
    }

    [Fact]
    public void Clicking_the_island_opens_the_slot_activity_page_or_the_last_page()
    {
        _island.OpenFor(_island.View.Slot);
        Assert.Equal(IslandPage.Media, _island.View.Page);
        _island.Open(IslandPage.Calendar);
        _island.Close();

        _island.OpenFor(_island.View.Slot);
        Assert.Equal(IslandPage.Calendar, _island.View.Page);
        _island.Close();

        _island.SetActivity(ActivityKind.Media, true);
        _island.OpenFor(_island.View.Slot);
        Assert.Equal(IslandPage.Media, _island.View.Page);
    }

    [Fact]
    public void NextDeadline_is_the_soonest_timed_end()
    {
        Assert.Null(_island.NextDeadline);
        _island.ShowCard(Reminder("todo", 10));
        Assert.Equal(_now.AddSeconds(10), _island.NextDeadline);
    }

    [Fact]
    public void Changed_fires_only_when_the_view_changes()
    {
        var count = 0;
        _island.Changed += _ => count++;
        _island.SetActivity(ActivityKind.Media, true);
        _island.SetActivity(ActivityKind.Media, true);
        _island.Tick();
        Assert.Equal(1, count);
    }
}
