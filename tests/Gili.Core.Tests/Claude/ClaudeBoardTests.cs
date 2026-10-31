using Gili.Core.Claude;

namespace Gili.Core.Tests.Claude;

/// <summary>F-CLD-02: session status from hooks and session files (PRD 6.3 "Sesi Claude", design 3h).</summary>
public sealed class ClaudeBoardTests
{
    private DateTimeOffset _now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private readonly ClaudeBoard _board;

    // The session's window is in front, so a Done is seen; unseen finishes are in ClaudeSeenTests.
    public ClaudeBoardTests() => _board = new ClaudeBoard(() => _now, _ => true);

    private void Hook(string hookEvent, string id = "s1", string? tool = null, string? type = null, string? message = null, string? question = null) =>
        _board.Apply(new ClaudeHookMessage(hookEvent, id, @"C:\Users\a\Documents\Project\top-taskbar", tool, type, message, question));

    private void File(string status, string id = "s1", int pid = 100, string? name = null, bool derived = false, string? waitingFor = null,
        string entrypoint = "cli") =>
        _board.Apply(new ClaudeSessionFile(pid, id, @"C:\Users\a\Documents\Project\top-taskbar", name, derived, entrypoint, status, waitingFor));

    private ClaudeSession S(string id = "s1") => _board.Sessions.Single(s => s.Id == id);

    private void Wait(double seconds)
    {
        _now += TimeSpan.FromSeconds(seconds);
        _board.Tick();
    }

    [Fact]
    public void A_new_session_is_idle_and_named_after_its_folder()
    {
        Hook("SessionStart");
        Assert.Equal(ClaudeStatus.Idle, S().Status);
        Assert.Equal("top-taskbar", S().Name);
        Assert.Equal(ClaudeApp.Terminal, S().App);
    }

    [Fact]
    public void A_prompt_starts_work()
    {
        Hook("SessionStart");
        Hook("UserPromptSubmit");
        Assert.Equal(ClaudeStatus.Working, S().Status);
        Assert.Equal(_now, S().PromptAt);
    }

    [Theory]
    [InlineData("AskUserQuestion", ClaudeNeed.Question)]
    [InlineData("ExitPlanMode", ClaudeNeed.Plan)]
    public void Questions_and_plans_need_input(string tool, ClaudeNeed need)
    {
        Hook("UserPromptSubmit");
        Hook("PreToolUse", tool: tool, question: "Which layout?");
        Assert.Equal(ClaudeStatus.NeedsInput, S().Status);
        Assert.Equal(need, S().Need);
    }

    [Fact]
    public void A_permission_prompt_needs_input_with_its_message()
    {
        Hook("UserPromptSubmit");
        Hook("Notification", type: "permission_prompt", message: "Claude needs your permission to use Bash");
        Assert.Equal(ClaudeNeed.Permission, S().Need);
        Assert.Equal("Claude needs your permission to use Bash", S().Snippet);
    }

    [Fact]
    public void Other_notifications_change_nothing()
    {
        Hook("UserPromptSubmit");
        Hook("Notification", type: "idle_prompt", message: "Claude is waiting for your input");
        Assert.Equal(ClaudeStatus.Working, S().Status);
    }

    [Fact]
    public void An_answer_goes_back_to_work()
    {
        Hook("UserPromptSubmit");
        Hook("PreToolUse", tool: "AskUserQuestion", question: "Which layout?");
        Hook("PostToolUse", tool: "AskUserQuestion");
        Assert.Equal(ClaudeStatus.Working, S().Status);
    }

    [Fact]
    public void Stop_is_done_for_four_seconds_then_idle()
    {
        Hook("UserPromptSubmit");
        _now += TimeSpan.FromSeconds(252);
        Hook("Stop");
        Assert.Equal(ClaudeStatus.Done, S().Status);
        Assert.Equal(TimeSpan.FromSeconds(252), S().DoneAfter);
        Wait(3.9);
        Assert.Equal(ClaudeStatus.Done, S().Status);
        Wait(0.2);
        Assert.Equal(ClaudeStatus.Idle, S().Status);
    }

    [Fact]
    public void Stop_without_a_prompt_is_not_done()
    {
        Hook("SessionStart");
        Hook("Stop");
        Assert.Equal(ClaudeStatus.Idle, S().Status);
    }

    [Fact]
    public void Esc_is_idle_without_done_once_stop_never_comes()
    {
        Hook("UserPromptSubmit");
        File("idle");
        Assert.Equal(ClaudeStatus.Working, S().Status);
        Wait(1.6);
        Assert.Equal(ClaudeStatus.Idle, S().Status);
    }

    [Fact]
    public void A_stop_right_after_the_idle_file_is_still_done()
    {
        Hook("UserPromptSubmit");
        File("idle");
        Wait(0.5);
        Hook("Stop");
        Assert.Equal(ClaudeStatus.Done, S().Status);
    }

    [Fact]
    public void Session_end_removes_the_session()
    {
        Hook("SessionStart");
        Hook("SessionEnd");
        Assert.Empty(_board.Sessions);
    }

    [Fact]
    public void A_session_that_ends_right_after_stop_still_shows_done()
    {
        // claude -p sends SessionEnd straight after Stop.
        Hook("UserPromptSubmit");
        Hook("Stop");
        Hook("SessionEnd");
        Assert.Equal(ClaudeStatus.Done, S().Status);
        Wait(4.1);
        Assert.Empty(_board.Sessions);
    }

    [Fact]
    public void A_dead_process_keeps_its_done_until_it_ends()
    {
        File("busy", pid: 100);
        Hook("Stop");
        _board.RemoveDead(_ => false);
        Assert.Equal(ClaudeStatus.Done, S().Status);
        Wait(4.1);
        _board.RemoveDead(_ => false);
        Assert.Empty(_board.Sessions);
    }

    [Fact]
    public void A_dead_process_removes_its_session()
    {
        File("idle", pid: 100);
        File("idle", id: "s2", pid: 200);
        _board.RemoveDead(pid => pid == 200);
        Assert.Equal(["s2"], _board.Sessions.Select(s => s.Id));
    }

    [Fact]
    public void A_busy_file_shows_a_session_that_started_before_gili()
    {
        File("busy");
        Assert.Equal(ClaudeStatus.Working, S().Status);
    }

    [Fact]
    public void A_waiting_file_while_working_needs_permission()
    {
        Hook("UserPromptSubmit");
        File("waiting", waitingFor: "approve Bash");
        Assert.Equal(ClaudeStatus.NeedsInput, S().Status);
        Assert.Equal(ClaudeNeed.Permission, S().Need);
    }

    [Fact]
    public void A_dialog_the_user_opened_while_idle_needs_nothing()
    {
        Hook("SessionStart");
        File("waiting", waitingFor: "dialog open");
        Assert.Equal(ClaudeStatus.Idle, S().Status);
    }

    [Fact]
    public void A_waiting_file_keeps_the_hooks_question()
    {
        Hook("UserPromptSubmit");
        Hook("PreToolUse", tool: "AskUserQuestion", question: "Which layout?");
        File("waiting", waitingFor: "input needed");
        Assert.Equal(ClaudeNeed.Question, S().Need);
        Assert.Equal("Which layout?", S().Snippet);
    }

    [Fact]
    public void Desktop_titles_name_the_session_but_derived_names_do_not()
    {
        File("idle", name: "Desain PCB LED", entrypoint: "claude-desktop");
        File("idle", id: "s2", pid: 200, name: "gili-6b", derived: true);
        Assert.Equal("Desain PCB LED", S().Name);
        Assert.Equal(ClaudeApp.Desktop, S().App);
        Assert.Equal("top-taskbar", S("s2").Name);
    }

    [Fact]
    public void Clear_forgets_every_session()
    {
        Hook("UserPromptSubmit");
        _board.Clear();
        Assert.Empty(_board.Sessions);
        Assert.Null(_board.NextDeadline);
    }

    [Fact]
    public void Changes_are_announced_once_per_change()
    {
        var count = 0;
        _board.Changed += () => count++;
        Hook("UserPromptSubmit");
        Hook("UserPromptSubmit");
        Assert.Equal(1, count);
    }

    [Fact]
    public void Next_deadline_covers_done_and_the_idle_wait()
    {
        Assert.Null(_board.NextDeadline);
        Hook("UserPromptSubmit");
        File("idle");
        Assert.Equal(_now + TimeSpan.FromSeconds(1.5), _board.NextDeadline);
        Hook("Stop");
        Assert.Equal(_now + TimeSpan.FromSeconds(4), _board.NextDeadline);
    }
}
