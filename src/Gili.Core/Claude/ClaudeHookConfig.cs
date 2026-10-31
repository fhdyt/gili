using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Gili.Core.Claude;

/// <summary>~/.claude/settings.json could not be read as a JSON object, so Gili leaves it alone (PRD 11).</summary>
public sealed class ClaudeSettingsException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Gili's hooks in Claude Code's settings.json (F-CLD-01, PRD 9.2): text in, text out, so every rule is tested. Gili's
/// entries are the ones whose command names the helper; every other hook and field passes through untouched.
/// </summary>
public static class ClaudeHookConfig
{
    /// <summary>The helper's file name and the Store copy's app execution alias.</summary>
    private static readonly string[] Markers = ["Gili.ClaudeHook", "gili-claude-hook"];

    /// <summary>Each hook Gili listens to and its matcher (PRD 9.2 "Hook Claude Code").</summary>
    private static readonly (string Event, string? Matcher)[] Hooks =
    [
        ("SessionStart", null),
        ("UserPromptSubmit", null),
        ("PreToolUse", "AskUserQuestion|ExitPlanMode"),
        ("Notification", null),
        ("PostToolUse", "*"),
        ("Stop", null),
        // A turn that ended on a rate limit: the 5-hour window's "Limit reached" (design 3j).
        ("StopFailure", "rate_limit"),
        ("SessionEnd", null),
    ];

    private const int TimeoutSeconds = 2;

    private static readonly JsonSerializerOptions Write = new()
    {
        WriteIndented = true,
        IndentSize = 2,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Adds Gili's hooks, replacing any it added before (another path included). <paramref name="json"/> null or
    /// blank is a file that does not exist yet.
    /// </summary>
    public static string Install(string? json, string helperPath)
    {
        var root = Parse(json);
        RemoveGili(root);
        var hooks = root["hooks"] as JsonObject ?? new JsonObject();
        root["hooks"] = hooks;
        foreach (var (hookEvent, matcher) in Hooks)
        {
            if (hooks[hookEvent] is not JsonArray entries)
            {
                entries = [];
                hooks[hookEvent] = entries;
            }
            var entry = new JsonObject();
            if (matcher is not null) entry["matcher"] = matcher;
            entry["hooks"] = new JsonArray(new JsonObject
            {
                ["type"] = "command",
                ["command"] = Command(helperPath, hookEvent),
                ["timeout"] = TimeoutSeconds,
            });
            entries.Add(entry);
        }
        return root.ToJsonString(Write);
    }

    /// <summary>Takes Gili's hooks out; event lists and a hooks object left empty go too.</summary>
    public static string Uninstall(string json)
    {
        var root = Parse(json);
        RemoveGili(root);
        return root.ToJsonString(Write);
    }

    public static bool IsInstalled(string? json)
    {
        try
        {
            return Parse(json)["hooks"] is JsonObject hooks && hooks.Any(h => h.Value is JsonArray entries && entries.Any(IsGiliEntry));
        }
        catch (ClaudeSettingsException)
        {
            return false;
        }
    }

    /// <summary>The quoted helper path with forward slashes, as the wmux hooks write it, then the event name.</summary>
    public static string Command(string helperPath, string hookEvent) => $"\"{helperPath.Replace('\\', '/')}\" {hookEvent}";

    private static JsonObject Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException ex)
        {
            throw new ClaudeSettingsException("settings.json is not valid JSON", ex);
        }
        if (node is not JsonObject root) throw new ClaudeSettingsException("settings.json is not a JSON object");
        if (root["hooks"] is { } hooks and not JsonObject) throw new ClaudeSettingsException("settings.json has hooks that are not an object");
        return root;
    }

    private static void RemoveGili(JsonObject root)
    {
        if (root["hooks"] is not JsonObject hooks) return;
        foreach (var name in hooks.Select(h => h.Key).ToList())
        {
            if (hooks[name] is not JsonArray entries) continue;
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i] is not JsonObject entry || entry["hooks"] is not JsonArray commands) continue;
                for (var j = commands.Count - 1; j >= 0; j--)
                {
                    if (IsGiliCommand(commands[j])) commands.RemoveAt(j);
                }
                if (commands.Count == 0) entries.RemoveAt(i);
            }
            if (entries.Count == 0) hooks.Remove(name);
        }
        if (hooks.Count == 0) root.Remove("hooks");
    }

    private static bool IsGiliEntry(JsonNode? entry) => entry?["hooks"] is JsonArray commands && commands.Any(IsGiliCommand);

    private static bool IsGiliCommand(JsonNode? hook) =>
        hook?["command"] is JsonValue value && value.TryGetValue(out string? command)
        && Markers.Any(m => command.Contains(m, StringComparison.OrdinalIgnoreCase));
}
