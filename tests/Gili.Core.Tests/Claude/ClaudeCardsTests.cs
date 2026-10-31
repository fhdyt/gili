using Gili.Core.Claude;

namespace Gili.Core.Tests.Claude;

/// <summary>F-CLD-03: the Needs input card (PRD 6.3 "Kartu Needs input", design 3i).</summary>
public sealed class ClaudeCardsTests
{
    private readonly DateTimeOffset _now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private readonly ClaudeBoard _board;
    private readonly ClaudeCards _cards = new();

    public ClaudeCardsTests() => _board = new ClaudeBoard(() => _now);

    private void Hook(string hookEvent, string id = "s1", string? tool = null, string? type = null, string? message = null, string? question = null) =>
        _board.Apply(new ClaudeHookMessage(hookEvent, id, @"C:\p\" + id, tool, type, message, question));

    private void File(string status, string id = "s1", string? waitingFor = null, string entry = "cli", string? name = null) =>
        _board.Apply(new ClaudeSessionFile(100, id, @"C:\p\" + id, name, false, entry, status, waitingFor));

    private ClaudeCardChange Update(bool fullscreen = false) => _cards.Update(_board.Sessions, fullscreen);

    private void Ask(string id = "s1", string question = "Which island layout do you prefer for the session list?")
    {
        Hook("UserPromptSubmit", id);
        Hook("PreToolUse", id, tool: "AskUserQuestion", question: question);
    }

    [Fact]
    public void A_question_shows_the_card()
    {
        Ask();
        Assert.Equal(ClaudeCardChange.Show, Update());
        var card = _cards.Current!;
        Assert.Equal("Claude needs your input", card.Title);
        Assert.Equal("s1", card.Name);
        Assert.Equal("Which island layout do you prefer for the session list?", card.Quote);
        Assert.Equal("Open terminal", card.OpenLabel);
    }

    [Fact]
    public void Permission_and_plan_titles()
    {
        Hook("UserPromptSubmit");
        Hook("Notification", type: "permission_prompt", message: "Claude needs your permission to use Bash");
        Update();
        Assert.Equal(("Claude needs permission", "Claude needs your permission to use Bash"), (_cards.Current!.Title, _cards.Current.Quote));
        Hook("PostToolUse");
        Hook("PreToolUse", tool: "ExitPlanMode");
        Update();
        Assert.Equal(("Plan ready for review", (string?)null), (_cards.Current!.Title, _cards.Current.Quote));
    }

    [Fact]
    public void A_desktop_session_opens_claude()
    {
        File("idle", entry: "claude-desktop", name: "Integrate taskbar");
        Ask();
        Update();
        Assert.Equal("Open Claude", _cards.Current!.OpenLabel);
        Assert.Equal("Integrate taskbar", _cards.Current.Name);
    }

    [Fact]
    public void The_quote_stops_at_90_characters()
    {
        Ask(question: new string('a', 120));
        Update();
        Assert.Equal(90, _cards.Current!.Quote!.Length);
        Assert.EndsWith("…", _cards.Current.Quote);
    }

    [Fact]
    public void Same_wait_never_cards_twice()
    {
        Hook("UserPromptSubmit");
        File("waiting", waitingFor: "Bash");
        Assert.Equal(ClaudeCardChange.Show, Update());
        _cards.Close();
        Hook("Notification", type: "permission_prompt", message: "Claude needs your permission to use Bash");
        Assert.Equal(ClaudeCardChange.None, Update());
        Assert.Null(_cards.Current);
    }

    [Fact]
    public void The_hooks_message_fills_in_the_card_on_show()
    {
        Hook("UserPromptSubmit");
        File("waiting", waitingFor: "Bash");
        Update();
        Assert.Null(_cards.Current!.Quote);
        Hook("Notification", type: "permission_prompt", message: "Claude needs your permission to use Bash");
        Assert.Equal(ClaudeCardChange.Refresh, Update());
        Assert.Equal("Claude needs your permission to use Bash", _cards.Current!.Quote);
    }

    [Fact]
    public void Answered_wait_dismisses_card()
    {
        Ask();
        Update();
        Hook("PostToolUse");
        Assert.Equal(ClaudeCardChange.Dismiss, Update());
        Assert.Null(_cards.Current);
    }

    [Fact]
    public void A_new_question_from_another_session_takes_the_card()
    {
        Ask("s1");
        Update();
        Ask("s2", "Ship it?");
        Assert.Equal(ClaudeCardChange.Show, Update());
        Assert.Equal("s2", _cards.Current!.SessionId);
        // The first session answering leaves the second's card alone.
        Hook("PostToolUse", "s1");
        Assert.Equal(ClaudeCardChange.None, Update());
    }

    [Fact]
    public void A_later_question_in_the_same_session_shows_again()
    {
        Ask();
        Update();
        _cards.Close();
        Hook("PostToolUse");
        Update();
        Hook("PreToolUse", tool: "AskUserQuestion", question: "And this?");
        Assert.Equal(ClaudeCardChange.Show, Update());
    }

    [Fact]
    public void Fullscreen_skips_new_wait()
    {
        Ask();
        Assert.Equal(ClaudeCardChange.None, Update(fullscreen: true));
        Assert.Equal(ClaudeCardChange.None, Update());
        Assert.Null(_cards.Current);
    }

    [Fact]
    public void A_session_that_stops_takes_its_card()
    {
        Ask();
        Update();
        Hook("Stop");
        Assert.Equal(ClaudeCardChange.Dismiss, Update());
    }
}
