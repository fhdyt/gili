using Gili.Core.Claude;

namespace Gili.Core.Tests.Claude;

/// <summary>F-CLD-03, F-CLD-04: unseen finishes and numbered waits on the board (design 3i).</summary>
public sealed class ClaudeSeenTests
{
    private const string Cwd = @"C:\Users\a\Documents\Project\top-taskbar";
    private DateTimeOffset _now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private bool _front;
    private readonly ClaudeBoard _board;

    public ClaudeSeenTests() => _board = new ClaudeBoard(() => _now, _ => _front);

    private void Hook(string hookEvent, string id = "s1", string? tool = null, string? type = null, string? message = null, string? question = null) =>
        _board.Apply(new ClaudeHookMessage(hookEvent, id, Cwd, tool, type, message, question));

    private void File(string status, string id = "s1", string? waitingFor = null) =>
        _board.Apply(new ClaudeSessionFile(100, id, Cwd, null, false, "cli", status, waitingFor));

    private ClaudeSession S(string id = "s1") => _board.Sessions.Single(s => s.Id == id);

    private void Wait(double seconds)
    {
        _now += TimeSpan.FromSeconds(seconds);
        _board.Tick();
    }

    private void Finish(string id = "s1")
    {
        Hook("UserPromptSubmit", id);
        Wait(30);
        Hook("Stop", id);
    }

    [Fact]
    public void Unseen_done_stays_done_after_four_seconds()
    {
        Finish();
        Assert.True(S().Unseen);
        Assert.True(S().ShowsDone);
        Wait(4);
        Assert.Equal(ClaudeStatus.Done, S().Status);
        Assert.False(S().ShowsDone);
    }

    [Fact]
    public void A_done_seen_in_front_goes_idle_after_four_seconds()
    {
        _front = true;
        Finish();
        Assert.False(S().Unseen);
        var finished = _now;
        Wait(4);
        Assert.Equal(ClaudeStatus.Idle, S().Status);
        Assert.Equal(finished, S().IdleSince);
    }

    [Fact]
    public void Seeing_a_finished_session_makes_it_idle_since_it_finished()
    {
        Finish();
        var finished = _now;
        Wait(60);
        _board.MarkSeen("s1");
        Assert.False(S().Unseen);
        Assert.Equal(ClaudeStatus.Idle, S().Status);
        Assert.Equal(finished, S().IdleSince);
    }

    [Fact]
    public void Seeing_it_during_its_four_seconds_keeps_the_done_on_show()
    {
        Finish();
        _board.MarkSeen(s => s.Id == "s1");
        Assert.True(S().ShowsDone);
        Wait(4);
        Assert.Equal(ClaudeStatus.Idle, S().Status);
    }

    [Fact]
    public void A_new_prompt_clears_unseen()
    {
        Finish();
        Wait(5);
        Hook("UserPromptSubmit");
        Assert.False(S().Unseen);
        Assert.Equal(ClaudeStatus.Working, S().Status);
    }

    [Fact]
    public void A_busy_file_after_an_unseen_done_starts_work()
    {
        Finish();
        Wait(5);
        File("busy");
        Assert.Equal(ClaudeStatus.Working, S().Status);
        Assert.False(S().Unseen);
    }

    [Fact]
    public void A_busy_file_during_the_done_on_show_changes_nothing()
    {
        Finish();
        File("busy");
        Assert.True(S().ShowsDone);
    }

    [Fact]
    public void An_ended_unseen_session_leaves_after_its_done()
    {
        Finish();
        Hook("SessionEnd");
        Assert.Single(_board.Sessions);
        Wait(4);
        Assert.Empty(_board.Sessions);
    }

    [Fact]
    public void Each_wait_gets_its_own_number()
    {
        Hook("UserPromptSubmit");
        Hook("PreToolUse", tool: "AskUserQuestion", question: "One?");
        var first = S().WaitId;
        Hook("PostToolUse");
        Hook("PreToolUse", tool: "AskUserQuestion", question: "Two?");
        Assert.True(first > 0);
        Assert.True(S().WaitId > first);
    }

    [Fact]
    public void A_hook_after_the_files_wait_keeps_its_number()
    {
        Hook("UserPromptSubmit");
        File("waiting", waitingFor: "Bash");
        var wait = S().WaitId;
        Hook("Notification", type: "permission_prompt", message: "Claude needs your permission to use Bash");
        Assert.Equal(wait, S().WaitId);
        Assert.Equal("Claude needs your permission to use Bash", S().Snippet);
    }

    [Fact]
    public void The_host_names_the_terminal()
    {
        Hook("SessionStart");
        _board.SetHost("s1", "Windows Terminal");
        Assert.Equal("Windows Terminal", ClaudeSlot.AppName(S()));
    }
}
