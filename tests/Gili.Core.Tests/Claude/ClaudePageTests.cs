using Gili.Core.Claude;

namespace Gili.Core.Tests.Claude;

/// <summary>F-CLD-05, F-CLD-06: the Claude page's rows, the slot's click and dot, and the session's window (design 3i).</summary>
public sealed class ClaudePageTests
{
    private DateTimeOffset _now = new(2026, 10, 31, 14, 5, 0, TimeSpan.Zero);
    private readonly ClaudeBoard _board;

    public ClaudePageTests() => _board = new ClaudeBoard(() => _now);

    private void Hook(string hookEvent, string id, string? tool = null, string? question = null) =>
        _board.Apply(new ClaudeHookMessage(hookEvent, id, @"C:\Projects\" + id, tool, null, null, question));

    private void Wait(double seconds)
    {
        _now += TimeSpan.FromSeconds(seconds);
        _board.Tick();
    }

    [Fact]
    public void Rows_go_waiting_working_done_idle()
    {
        Hook("SessionStart", "idle");
        Hook("UserPromptSubmit", "done");
        Hook("UserPromptSubmit", "work");
        Hook("UserPromptSubmit", "ask");
        Hook("PreToolUse", "ask", "AskUserQuestion", "Which layout?");
        Wait(252);
        Hook("Stop", "done");
        Wait(5);

        var rows = ClaudePage.Rows(_board.Sessions, _now, use24Hour: true);

        Assert.Equal(["ask", "work", "done", "idle"], rows.Select(r => r.Id));
        Assert.Equal(("\"Which layout?\"", "Needs input"), (rows[0].Sub, rows[0].Right));
        Assert.Equal(("work · Terminal", "Working · 4:17"), (rows[1].Sub, rows[1].Right));
        Assert.Equal("Done · 4m 12s", rows[2].Right);
        Assert.Equal("Idle since 14:05", rows[3].Right);
        Assert.Equal(ClaudeRowKind.Idle, rows[3].Kind);
    }

    [Fact]
    public void Idle_time_follows_the_12_hour_clock()
    {
        Hook("SessionStart", "s");
        Assert.Equal("Idle since 2:05 PM", ClaudePage.Rows(_board.Sessions, _now, use24Hour: false)[0].Right);
    }

    [Fact]
    public void A_plan_without_text_says_so()
    {
        Hook("UserPromptSubmit", "s");
        Hook("PreToolUse", "s", "ExitPlanMode");
        Assert.Equal("Plan ready for review", ClaudePage.Rows(_board.Sessions, _now, true)[0].Sub);
    }

    [Fact]
    public void The_slot_click_is_about_the_waiting_then_the_done_then_the_working()
    {
        Hook("UserPromptSubmit", "a");
        Hook("UserPromptSubmit", "b");
        Assert.Equal(["a", "b"], ClaudeSlot.Involved(_board.Sessions).Select(s => s.Id));
        Hook("Stop", "a");
        Assert.Equal(["a"], ClaudeSlot.Involved(_board.Sessions).Select(s => s.Id));
        Hook("PreToolUse", "b", "AskUserQuestion", "Q?");
        Assert.Equal(["b"], ClaudeSlot.Involved(_board.Sessions).Select(s => s.Id));
    }

    [Fact]
    public void Another_unseen_finish_puts_a_dot_on_the_slot()
    {
        Hook("UserPromptSubmit", "a");
        Hook("UserPromptSubmit", "b");
        Hook("Stop", "a");
        // a's Done is on show: no dot for it.
        Assert.False(ClaudeSlot.For(_board.Sessions, _now).Dot);
        Wait(4);
        var slot = ClaudeSlot.For(_board.Sessions, _now);
        Assert.Equal(ClaudeSlotState.Working, slot.State);
        Assert.True(slot.Dot);
        Assert.Equal("Claude · 1 working", ClaudeSlot.LauncherTooltip(_board.Sessions));
        Hook("Stop", "b");
        Wait(4);
        Assert.Equal(ClaudeSlotState.None, ClaudeSlot.For(_board.Sessions, _now).State);
        Assert.Equal("Claude · 2 finished", ClaudeSlot.LauncherTooltip(_board.Sessions));
        Assert.Equal(("a\nb", "2 Claude sessions finished"), ClaudeSlot.Bubble(ClaudeSlot.Unseen(_board.Sessions)));
    }

    private static readonly Dictionary<int, ProcessFacts> Tree = new()
    {
        [10] = new(20, 300, "claude.exe"),
        [20] = new(30, 200, "pwsh.exe"),
        [30] = new(1, 100, "WindowsTerminal.exe"),
        [40] = new(50, 300, "claude.exe"),
        [50] = new(1, 400, "reused.exe"),
        [60] = new(70, 300, "pwsh.exe"),
        [70] = new(1, 100, "explorer.exe"),
    };

    private static ProcessFacts? Facts(int pid) => Tree.TryGetValue(pid, out var f) ? f : null;

    [Fact]
    public void The_window_is_the_first_parent_with_one()
    {
        Assert.Equal(((nint)3000, 30), ClaudeWindows.Find(10, Facts, pid => pid is 30 or 50 or 70 ? pid * 100 : 0));
    }

    [Fact]
    public void Stops_when_parent_started_after_child() =>
        Assert.Null(ClaudeWindows.Find(40, Facts, pid => pid == 50 ? 5000 : 0));

    [Fact]
    public void Explorers_windows_never_count() =>
        Assert.Null(ClaudeWindows.Find(60, Facts, pid => pid == 70 ? 7000 : 0));

    [Fact]
    public void An_unreadable_start_time_trusts_the_parent()
    {
        var tree = new Dictionary<int, ProcessFacts> { [1] = new(2, 0, "claude.exe"), [2] = new(0, 500, "WindowsTerminal.exe") };
        Assert.Equal(((nint)22, 2), ClaudeWindows.Find(1, p => tree.TryGetValue(p, out var f) ? f : null, p => p == 2 ? 22 : 0));
    }

    [Fact]
    public void No_pid_no_window() => Assert.Null(ClaudeWindows.Find(-1, Facts, _ => 1));
}
