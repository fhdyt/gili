using Gili.Core.Claude;

namespace Gili.Core.Tests.Claude;

/// <summary>The compact slot's Claude activity, as the mockup's clSlotHTML and clTip write it.</summary>
public sealed class ClaudeSlotTests
{
    private DateTimeOffset _now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private readonly ClaudeBoard _board;

    public ClaudeSlotTests() => _board = new ClaudeBoard(() => _now);

    private void Hook(string hookEvent, string id, string folder = "top-taskbar", string? tool = null, string? question = null) =>
        _board.Apply(new ClaudeHookMessage(hookEvent, id, @"C:\p\" + folder, tool, null, null, question));

    private ClaudeSlotView Slot() => ClaudeSlot.For(_board.Sessions, _now);

    [Fact]
    public void Launcher_tooltip_counts_like_the_mockup()
    {
        Assert.Equal("Claude sessions", ClaudeSlot.LauncherTooltip(_board.Sessions));
        Hook("UserPromptSubmit", "a");
        Hook("UserPromptSubmit", "b");
        Assert.Equal("Claude · 2 working", ClaudeSlot.LauncherTooltip(_board.Sessions));
        Hook("PreToolUse", "a", tool: "AskUserQuestion");
        Assert.Equal("Claude · 1 needs input", ClaudeSlot.LauncherTooltip(_board.Sessions));
        Hook("PreToolUse", "b", tool: "AskUserQuestion");
        Assert.Equal("Claude · 2 need input", ClaudeSlot.LauncherTooltip(_board.Sessions));
    }

    [Fact]
    public void Nothing_running_shows_nothing() => Assert.Equal(ClaudeSlotState.None, Slot().State);

    [Fact]
    public void One_working_session_shows_its_name_and_time()
    {
        Hook("UserPromptSubmit", "a");
        _now += TimeSpan.FromSeconds(134);
        var slot = Slot();
        Assert.Equal(ClaudeSlotState.Working, slot.State);
        Assert.Equal("top-taskbar", slot.Name);
        Assert.Equal("2:14", slot.Value);
    }

    [Fact]
    public void Working_past_an_hour_shows_hours()
    {
        Hook("UserPromptSubmit", "a");
        _now += TimeSpan.FromSeconds(3725);
        Assert.Equal("1:02:05", Slot().Value);
    }

    [Fact]
    public void Two_working_sessions_are_counted()
    {
        Hook("UserPromptSubmit", "a");
        Hook("UserPromptSubmit", "b");
        Assert.Equal("2 sessions working", Slot().Text);
        Assert.Null(Slot().Name);
    }

    [Fact]
    public void Needs_input_wins_over_working()
    {
        Hook("UserPromptSubmit", "a");
        Hook("UserPromptSubmit", "b", folder: "Integrasi taskbar dengan Claude Code");
        Hook("PreToolUse", "b", folder: "Integrasi taskbar dengan Claude Code", tool: "AskUserQuestion", question: "Which layout?");
        var slot = Slot();
        Assert.Equal(ClaudeSlotState.NeedsInput, slot.State);
        Assert.Equal("Integrasi taskbar dengan Cl…", slot.Name);
        Assert.Equal("Needs input", slot.Text);
        Assert.Equal("Integrasi taskbar dengan Claude Code\nC:\\p\\Integrasi taskbar dengan Claude Code · Terminal\nWhich layout?", slot.Tooltip);
    }

    [Fact]
    public void Two_sessions_needing_input_are_counted()
    {
        Hook("PreToolUse", "a", tool: "AskUserQuestion");
        Hook("PreToolUse", "b", tool: "ExitPlanMode");
        Assert.Equal("2 sessions need input", Slot().Text);
    }

    [Theory]
    [InlineData(38, "Done · 38s")]
    [InlineData(252, "Done · 4m 12s")]
    [InlineData(3900, "Done · 1h 5m")]
    public void Done_shows_how_long_it_took(int seconds, string text)
    {
        Hook("UserPromptSubmit", "a");
        _now += TimeSpan.FromSeconds(seconds);
        Hook("Stop", "a");
        Assert.Equal(ClaudeSlotState.Done, Slot().State);
        Assert.Equal(text, Slot().Text);
    }

    [Fact]
    public void Done_wins_over_working_but_not_over_needs_input()
    {
        Hook("UserPromptSubmit", "a");
        Hook("UserPromptSubmit", "b");
        Hook("Stop", "a");
        Assert.Equal(ClaudeSlotState.Done, Slot().State);
        Hook("PreToolUse", "b", tool: "AskUserQuestion");
        Assert.Equal(ClaudeSlotState.NeedsInput, Slot().State);
    }
}
