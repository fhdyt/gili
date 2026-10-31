using Gili.Core.Devices;
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

    private static WideEvent Wide(string key, double seconds = 3, string? text = null) =>
        new(key, WideIcon.None, WideTone.Text, [new WideRun(text ?? key)], TimeSpan.FromSeconds(seconds));

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
        _island.ShowWide(Wide("charger", 2, "On battery"));

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
        _island.ShowWide(Wide("caps", 1.5, "Caps Lock off"));
        Advance(1);
        Assert.Equal("Caps Lock off", _island.View.Wide?.Text);
    }

    [Fact]
    public void Bluetooth_flapping_keeps_only_the_latest()
    {
        var buds = new BluetoothChange("buds", "Galaxy Buds2 Pro", BluetoothKind.Audio, Connected: true, Battery: null);
        _island.ShowWide(Wide("caps", 1.5));
        _island.ShowWide(BluetoothRules.WideFor(buds));
        _island.ShowWide(BluetoothRules.WideFor(buds with { Connected = false }));
        _island.ShowWide(BluetoothRules.WideFor(buds));

        Advance(1.5);
        Assert.Equal("Galaxy Buds2 Pro Connected", _island.View.Wide?.Text);
        Advance(4);
        Assert.Equal(IslandMode.Compact, _island.View.Mode);
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
        Assert.True(_island.HasCard("todo"));
        Advance(0.1);
        Assert.Equal(IslandMode.Compact, _island.View.Mode);
        Assert.False(_island.HasCard("todo"));
    }

    [Fact]
    public void A_waiting_card_is_still_held()
    {
        _island.ShowCard(Urgent("alarm"));
        _island.ShowCard(Reminder("todo", 10));
        Assert.True(_island.HasCard("todo"));
        Assert.False(_island.HasCard("other"));
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
    public void Focus_holds_reminder_cards_until_it_ends()
    {
        _island.SetFocus(true);
        _island.ShowCard(Reminder("todo"));
        Assert.NotEqual(IslandMode.Card, _island.View.Mode);
        Advance(60);
        _island.SetFocus(false);
        Assert.Equal(("todo", IslandMode.Card), (_island.View.Card!.Id, _island.View.Mode));
    }

    [Fact]
    public void Focus_never_holds_urgent_cards()
    {
        _island.SetFocus(true);
        _island.ShowCard(Urgent("timer"));
        Assert.Equal(IslandMode.Card, _island.View.Mode);
    }

    [Fact]
    public void Clipboard_snippet_holds_the_slot_for_3_seconds()
    {
        _island.ShowClipboard();
        Assert.Equal(ActivityKind.ClipboardSnippet, _island.View.Slot);
        Advance(2.9);
        Assert.Equal(ActivityKind.ClipboardSnippet, _island.View.Slot);
        Advance(0.2);
        Assert.Null(_island.View.Slot);
    }

    [Fact]
    public void A_new_copy_restarts_the_3_seconds()
    {
        _island.ShowClipboard();
        Advance(2);
        _island.ShowClipboard();
        Advance(2);
        Assert.Equal(ActivityKind.ClipboardSnippet, _island.View.Slot);
        Assert.Equal(_now.AddSeconds(1), _island.NextDeadline);
    }

    [Fact]
    public void A_snippet_moves_media_to_the_bubble()
    {
        _island.SetActivity(ActivityKind.Media, true);
        _island.ShowClipboard();
        Assert.Equal((ActivityKind.ClipboardSnippet, ActivityKind.Media), (_island.View.Slot!.Value, _island.View.Bubble!.Value));
        Advance(3.1);
        Assert.Equal(ActivityKind.Media, _island.View.Slot);
    }

    [Fact]
    public void Clicking_a_snippet_opens_the_clipboard_page()
    {
        _island.ShowClipboard();
        _island.OpenFor(_island.View.Slot);
        Assert.Equal((IslandMode.Expanded, IslandPage.Clipboard), (_island.View.Mode, _island.View.Page));
    }

    [Fact]
    public void No_snippet_in_fullscreen()
    {
        _island.SetFullscreen(true);
        _island.ShowClipboard();
        Assert.Equal(IslandMode.Hidden, _island.View.Mode);
        _island.SetFullscreen(false);
        Advance(3.1);
        Assert.Null(_island.View.Slot);
    }

    [Fact]
    public void Clicking_the_open_page_icon_closes_even_though_the_press_already_dismissed_the_island()
    {
        // Pressing a bar icon takes focus from the open island before the click arrives (PRD 6.3 buka/tutup).
        _island.Toggle(IslandPage.Controls);
        _island.Dismiss();
        Advance(0.2);
        _island.Toggle(IslandPage.Controls);
        Assert.Equal(IslandMode.Compact, _island.View.Mode);
    }

    [Fact]
    public void Another_page_icon_opens_its_page_right_after_a_dismiss()
    {
        _island.Toggle(IslandPage.Controls);
        _island.Dismiss();
        Advance(0.2);
        _island.Toggle(IslandPage.Calendar);
        Assert.Equal((IslandMode.Expanded, IslandPage.Calendar), (_island.View.Mode, _island.View.Page));
    }

    [Fact]
    public void The_same_icon_opens_again_once_the_dismiss_is_past()
    {
        _island.Toggle(IslandPage.Controls);
        _island.Dismiss();
        Advance(0.5);
        _island.Toggle(IslandPage.Controls);
        Assert.Equal((IslandMode.Expanded, IslandPage.Controls), (_island.View.Mode, _island.View.Page));
    }

    [Fact]
    public void Other_control_swaps_without_closing()
    {
        _island.Toggle(ControlKind.Power);
        _island.Toggle(ControlKind.Volume);
        Assert.Equal((IslandMode.Expanded, IslandPage.Controls, ControlKind.Volume), (_island.View.Mode, _island.View.Page, _island.View.Control));
    }

    [Fact]
    public void Same_control_closes()
    {
        _island.Toggle(ControlKind.Wifi);
        _island.Toggle(ControlKind.Wifi);
        Assert.Equal(IslandMode.Compact, _island.View.Mode);
    }

    [Fact]
    public void Dismissing_click_on_the_open_control_stays_closed()
    {
        _island.Toggle(ControlKind.Bluetooth);
        _island.Dismiss();
        Advance(0.2);
        _island.Toggle(ControlKind.Bluetooth);
        Assert.Equal(IslandMode.Compact, _island.View.Mode);
    }

    [Fact]
    public void Dismissing_click_on_another_control_opens_it()
    {
        _island.Toggle(ControlKind.Bluetooth);
        _island.Dismiss();
        Advance(0.2);
        _island.Toggle(ControlKind.Brightness);
        Assert.Equal((IslandMode.Expanded, ControlKind.Brightness), (_island.View.Mode, _island.View.Control));
    }

    [Fact]
    public void A_page_icon_replaces_a_control_island()
    {
        _island.Toggle(ControlKind.Volume);
        _island.Toggle(IslandPage.Calendar);
        Assert.Equal((IslandMode.Expanded, IslandPage.Calendar), (_island.View.Mode, _island.View.Page));
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

    [Fact]
    public void An_unseen_claude_finish_is_a_bubble_beside_the_compact_island()
    {
        _island.SetActivity(ActivityKind.Media, true);
        _island.SetClaudeUnseen(true);
        Assert.True(_island.View.DoneBubble);
        Assert.Equal(ActivityKind.Media, _island.View.Slot);
    }

    [Fact]
    public void No_claude_bubble_while_claude_shows_in_the_slot_or_the_bubble()
    {
        _island.SetClaudeUnseen(true);
        _island.SetActivity(ActivityKind.ClaudeWorking, true);
        Assert.False(_island.View.DoneBubble);
        _island.SetActivity(ActivityKind.Timer, true);
        Assert.Equal(ActivityKind.ClaudeWorking, _island.View.Bubble);
        Assert.False(_island.View.DoneBubble);
        _island.SetActivity(ActivityKind.Stopwatch, true);
        // Claude third: hidden, so the green bubble shows again.
        Assert.True(_island.View.DoneBubble);
    }

    [Fact]
    public void The_claude_bubble_only_shows_while_compact()
    {
        _island.SetClaudeUnseen(true);
        _island.Open(IslandPage.Media);
        Assert.False(_island.View.DoneBubble);
        _island.Close();
        _island.SetFullscreen(true);
        Assert.False(_island.View.DoneBubble);
    }

    [Fact]
    public void A_focus_phase_holds_the_claude_reset_card_but_not_a_timer()
    {
        _island.SetFocus(true);
        _island.ShowCard(new IslandCard("claude-reset", CardPriority.Urgent, TimeSpan.Zero, HoldInFocus: true));
        Assert.Equal(IslandMode.Compact, _island.View.Mode);
        _island.ShowCard(Urgent("timer"));
        Assert.Equal("timer", _island.View.Card!.Id);
        _island.DismissCard("timer");
        _island.SetFocus(false);
        Assert.Equal("claude-reset", _island.View.Card!.Id);
    }

    [Fact]
    public void A_reached_limit_follows_a_waiting_session_and_a_running_window_comes_last()
    {
        _island.SetActivity(ActivityKind.ClaudeWindow, true);
        _island.SetActivity(ActivityKind.Charging, true);
        Assert.Equal((ActivityKind.Charging, ActivityKind.ClaudeWindow), (_island.View.Slot!.Value, _island.View.Bubble!.Value));
        _island.SetActivity(ActivityKind.ClaudeWindow, false);
        _island.SetActivity(ActivityKind.ClaudeLimit, true);
        _island.SetActivity(ActivityKind.FileOperation, true);
        Assert.Equal(ActivityKind.ClaudeLimit, _island.View.Slot);
        _island.SetActivity(ActivityKind.ClaudeAttention, true);
        Assert.Equal((ActivityKind.ClaudeAttention, ActivityKind.ClaudeLimit), (_island.View.Slot!.Value, _island.View.Bubble!.Value));
    }

    [Fact]
    public void Files_on_the_shelf_outrank_running_activities_but_not_a_copy_claude_or_a_file_operation()
    {
        _island.SetActivity(ActivityKind.Media, true);
        _island.SetActivity(ActivityKind.Timer, true);
        _island.SetActivity(ActivityKind.Shelf, true);
        Assert.Equal((ActivityKind.Shelf, ActivityKind.Timer), (_island.View.Slot!.Value, _island.View.Bubble!.Value));

        _island.SetActivity(ActivityKind.FileOperation, true);
        Assert.Equal((ActivityKind.FileOperation, ActivityKind.Shelf), (_island.View.Slot!.Value, _island.View.Bubble!.Value));
        _island.SetActivity(ActivityKind.FileOperation, false);
        _island.ShowClipboard();
        Assert.Equal((ActivityKind.ClipboardSnippet, ActivityKind.Shelf), (_island.View.Slot!.Value, _island.View.Bubble!.Value));
    }

    [Fact]
    public void The_shelf_slot_opens_the_shelf_page()
    {
        _island.SetActivity(ActivityKind.Shelf, true);
        _island.OpenFor(_island.View.Slot);
        Assert.Equal((IslandMode.Expanded, IslandPage.Shelf), (_island.View.Mode, _island.View.Page));
    }
}
