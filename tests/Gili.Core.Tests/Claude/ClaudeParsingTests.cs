using Gili.Core.Claude;

namespace Gili.Core.Tests.Claude;

public sealed class ClaudeParsingTests
{
    [Fact]
    public void Session_files_read_as_claude_writes_them()
    {
        var file = ClaudeSessionFile.Parse("""
            {"pid":8392,"sessionId":"01d1ea7d","cwd":"C:\\Users\\a\\liquid-pixel","startedAt":1793447899489,"kind":"interactive",
             "entrypoint":"claude-desktop","hostSessionId":"local_ae","name":"Desain PCB LED SMD 4x4cm","nameSince":1,
             "status":"waiting","waitingFor":"approve plan","statusUpdatedAt":1793447900546}
            """);
        Assert.Equal(new ClaudeSessionFile(8392, "01d1ea7d", @"C:\Users\a\liquid-pixel", "Desain PCB LED SMD 4x4cm", false, "claude-desktop", "waiting", "approve plan"), file);
    }

    [Fact]
    public void Derived_names_are_marked()
    {
        var file = ClaudeSessionFile.Parse("""{"pid":1,"sessionId":"x","name":"gili-6b","nameSource":"derived","status":"busy"}""");
        Assert.True(file!.DerivedName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ broken")]
    [InlineData("""{"pid":1}""")]
    [InlineData("[]")]
    public void Unusable_files_read_as_nothing(string json) => Assert.Null(ClaudeSessionFile.Parse(json));

    [Fact]
    public void Pipe_messages_read_as_the_helper_writes_them()
    {
        var message = ClaudeHookMessage.Parse("""{"event":"PreToolUse","session_id":"s1","cwd":"C:\\p","tool":"AskUserQuestion","question":"Which?"}""");
        Assert.Equal(new ClaudeHookMessage("PreToolUse", "s1", @"C:\p", "AskUserQuestion", null, null, "Which?"), message);
    }

    [Theory]
    [InlineData("nope")]
    [InlineData("""{"event":"Stop"}""")]
    [InlineData("""{"session_id":"s"}""")]
    public void Pipe_messages_without_an_event_or_session_are_dropped(string line) => Assert.Null(ClaudeHookMessage.Parse(line));
}
