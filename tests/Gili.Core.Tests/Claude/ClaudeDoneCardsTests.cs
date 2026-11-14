using Gili.Core.Claude;

namespace Gili.Core.Tests.Claude;

/// <summary>F-CLD-09: the Claude is done card (revision 25, issue 42).</summary>
public sealed class ClaudeDoneCardsTests
{
    private DateTimeOffset _now = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
    private bool _front;
    private readonly ClaudeBoard _board;
    private readonly ClaudeDoneCards _cards = new();

    public ClaudeDoneCardsTests() => _board = new ClaudeBoard(() => _now, _ => _front);

    private void Hook(string hookEvent, string id = "s1") =>
        _board.Apply(new ClaudeHookMessage(hookEvent, id, @"C:\p\" + id, null, null, null, null));

    private ClaudeCardChange Update(bool fullscreen = false) => _cards.Update(_board.Sessions, fullscreen);

    private void Finish(string id = "s1", int seconds = 252)
    {
        Hook("UserPromptSubmit", id);
        _now += TimeSpan.FromSeconds(seconds);
        Hook("Stop", id);
    }

    [Fact]
    public void A_finish_behind_another_window_shows_the_card()
    {
        Finish();
        Assert.Equal(ClaudeCardChange.Show, Update());
        var card = _cards.Current!;
        Assert.Equal("s1", card.SessionId);
        Assert.Equal("Claude is done", card.Title);
        Assert.Equal("s1", card.Name);
        Assert.Equal("Finished in 4m 12s", card.Quote);
    }

    [Fact]
    public void A_finish_in_front_shows_no_card()
    {
        _front = true;
        Finish();
        Assert.Equal(ClaudeCardChange.None, Update());
        Assert.Null(_cards.Current);
    }

    [Fact]
    public void The_card_stays_after_the_done_in_the_slot_ends()
    {
        Finish();
        Update();
        _now += TimeSpan.FromSeconds(5);
        _board.Tick();
        Assert.Equal(ClaudeCardChange.None, Update());
        Assert.NotNull(_cards.Current);
    }

    [Fact]
    public void Seeing_the_session_dismisses_the_card()
    {
        Finish();
        Update();
        _board.MarkSeen("s1");
        Assert.Equal(ClaudeCardChange.Dismiss, Update());
        Assert.Null(_cards.Current);
    }

    [Fact]
    public void A_new_prompt_dismisses_the_card()
    {
        Finish();
        Update();
        Hook("UserPromptSubmit");
        Assert.Equal(ClaudeCardChange.Dismiss, Update());
    }

    [Fact]
    public void A_finish_opens_the_card_once()
    {
        Finish();
        Update();
        _cards.Close();
        Assert.Equal(ClaudeCardChange.None, Update());
        Assert.Null(_cards.Current);
    }

    [Fact]
    public void A_newer_finish_takes_the_card_over()
    {
        Finish("s1");
        Update();
        Finish("s2", 38);
        Assert.Equal(ClaudeCardChange.Show, Update());
        Assert.Equal("s2", _cards.Current!.SessionId);
        Assert.Equal("Finished in 38s", _cards.Current.Quote);
    }

    [Fact]
    public void The_next_finish_of_the_same_session_opens_it_again()
    {
        Finish();
        Update();
        _cards.Close();
        Finish();
        Assert.Equal(ClaudeCardChange.Show, Update());
    }

    [Fact]
    public void In_fullscreen_the_finish_is_skipped_not_kept()
    {
        Finish();
        Assert.Equal(ClaudeCardChange.None, Update(fullscreen: true));
        Assert.Equal(ClaudeCardChange.None, Update());
        Assert.Null(_cards.Current);
    }

    [Fact]
    public void A_session_that_goes_away_takes_its_card()
    {
        Finish();
        Update();
        Assert.Equal(ClaudeCardChange.Dismiss, _cards.Update([], fullscreen: false));
    }
}
