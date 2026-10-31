using System.Text.Json.Nodes;
using Gili.Core.Claude;

namespace Gili.Core.Tests.Claude;

/// <summary>F-CLD-01: Gili's hooks go in and out of ~/.claude/settings.json without touching anything else.</summary>
public sealed class ClaudeHookConfigTests
{
    private const string Helper = @"C:\Users\a\AppData\Local\Programs\Gili\Gili.ClaudeHook.exe";

    /// <summary>The user's real file has wmux hooks; they must come through untouched.</summary>
    private const string WithWmux = """
        {
          "model": "opus",
          "hooks": {
            "PostToolUse": [
              { "matcher": "Bash", "hooks": [ { "type": "command", "command": "node \"C:/wmux/wmux-hook.js\" Bash" } ] }
            ],
            "Notification": [
              { "hooks": [ { "type": "command", "command": "node \"C:/wmux/wmux-hook.js\" --event Notification" } ] }
            ]
          },
          "statusLine": { "type": "command", "command": "node -e \"x\"" }
        }
        """;

    private static JsonArray Entries(string json, string hookEvent) => JsonNode.Parse(json)!["hooks"]![hookEvent]!.AsArray();

    private static List<string> Commands(string json, string hookEvent) =>
        [.. Entries(json, hookEvent).SelectMany(e => e!["hooks"]!.AsArray()).Select(h => (string)h!["command"]!)];

    [Fact]
    public void Install_adds_every_hook_in_the_prd()
    {
        var json = ClaudeHookConfig.Install(WithWmux, Helper);
        foreach (var hookEvent in new[] { "SessionStart", "UserPromptSubmit", "PreToolUse", "Notification", "PostToolUse", "Stop", "SessionEnd" })
            Assert.Contains($"\"C:/Users/a/AppData/Local/Programs/Gili/Gili.ClaudeHook.exe\" {hookEvent}", Commands(json, hookEvent));
        Assert.True(ClaudeHookConfig.IsInstalled(json));
    }

    [Fact]
    public void Matchers_follow_the_prd()
    {
        var json = ClaudeHookConfig.Install(null, Helper);
        Assert.Equal("AskUserQuestion|ExitPlanMode", (string)Entries(json, "PreToolUse")[0]!["matcher"]!);
        Assert.Equal("*", (string)Entries(json, "PostToolUse")[0]!["matcher"]!);
        Assert.Null(Entries(json, "Stop")[0]!["matcher"]);
        Assert.Equal(2, (int)Entries(json, "Stop")[0]!["hooks"]![0]!["timeout"]!);
    }

    [Fact]
    public void Other_hooks_and_fields_stay_as_they_were()
    {
        var json = ClaudeHookConfig.Install(WithWmux, Helper);
        var root = JsonNode.Parse(json)!;
        Assert.Equal("opus", (string)root["model"]!);
        Assert.Equal("node -e \"x\"", (string)root["statusLine"]!["command"]!);
        Assert.Contains("node \"C:/wmux/wmux-hook.js\" Bash", Commands(json, "PostToolUse"));
        Assert.Equal("Bash", (string)Entries(json, "PostToolUse")[0]!["matcher"]!);
        Assert.Contains("node \"C:/wmux/wmux-hook.js\" --event Notification", Commands(json, "Notification"));
    }

    [Fact]
    public void Installing_twice_adds_nothing_more()
    {
        var once = ClaudeHookConfig.Install(WithWmux, Helper);
        var twice = ClaudeHookConfig.Install(once, Helper);
        Assert.Equal(once, twice);
    }

    [Fact]
    public void A_new_helper_path_replaces_the_old_one()
    {
        var old = ClaudeHookConfig.Install(WithWmux, @"D:\old\Gili.ClaudeHook.exe");
        var json = ClaudeHookConfig.Install(old, Helper);
        Assert.DoesNotContain(Commands(json, "Stop"), c => c.Contains("D:/old", StringComparison.Ordinal));
        Assert.Single(Commands(json, "Stop"), c => c.Contains("Gili.ClaudeHook", StringComparison.Ordinal));
    }

    [Fact]
    public void Uninstall_takes_out_only_gilis_hooks()
    {
        var json = ClaudeHookConfig.Uninstall(ClaudeHookConfig.Install(WithWmux, Helper));
        Assert.False(ClaudeHookConfig.IsInstalled(json));
        Assert.Equal(["node \"C:/wmux/wmux-hook.js\" Bash"], Commands(json, "PostToolUse"));
        Assert.Equal(["node \"C:/wmux/wmux-hook.js\" --event Notification"], Commands(json, "Notification"));
        Assert.Null(JsonNode.Parse(json)!["hooks"]!["SessionStart"]);
    }

    [Fact]
    public void Uninstall_leaves_no_empty_hooks_behind()
    {
        var json = ClaudeHookConfig.Uninstall(ClaudeHookConfig.Install("""{ "model": "opus" }""", Helper));
        Assert.Null(JsonNode.Parse(json)!["hooks"]);
        Assert.Equal("opus", (string)JsonNode.Parse(json)!["model"]!);
    }

    [Fact]
    public void The_store_alias_counts_as_gilis_hook() =>
        Assert.False(ClaudeHookConfig.IsInstalled(ClaudeHookConfig.Uninstall(
            ClaudeHookConfig.Install(null, @"C:\Users\a\AppData\Local\Microsoft\WindowsApps\gili-claude-hook.exe"))));

    [Theory]
    [InlineData("{ not json")]
    [InlineData("[1, 2]")]
    [InlineData("{ \"hooks\": [] }")]
    public void A_file_gili_cannot_read_is_refused(string text)
    {
        Assert.Throws<ClaudeSettingsException>(() => ClaudeHookConfig.Install(text, Helper));
        Assert.Throws<ClaudeSettingsException>(() => ClaudeHookConfig.Uninstall(text));
    }

    [Fact]
    public void Non_ascii_text_stays_readable()
    {
        var json = ClaudeHookConfig.Install("""{ "note": "Désain — ✓" }""", Helper);
        Assert.Contains("Désain — ✓", json, StringComparison.Ordinal);
    }
}
