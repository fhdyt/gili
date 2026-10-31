using System.Text.Json;

namespace Gili.Core.Claude;

/// <summary>A Claude Code session's status (PRD 6.3 "Status tiap sesi").</summary>
public enum ClaudeStatus { Idle, Working, NeedsInput, Done }

/// <summary>What a waiting session needs: an answer, a permission, or a plan review (PRD 6.3 "Kartu Needs input").</summary>
public enum ClaudeNeed { Question, Permission, Plan }

/// <summary>Where the session runs: the Code tab of Claude Desktop or `claude` in a terminal.</summary>
public enum ClaudeApp { Terminal, Desktop }

/// <summary>
/// One line from <c>Gili.ClaudeHook</c> over the pipe (PRD 9.2 "Hook Claude Code"). The prompt's text is never sent;
/// <paramref name="Question"/> is the first question of AskUserQuestion, at most 200 characters.
/// </summary>
public sealed record ClaudeHookMessage(
    string Event,
    string SessionId,
    string? Cwd,
    string? Tool,
    string? NotificationType,
    string? Message,
    string? Question)
{
    /// <summary>Null for anything that is not a JSON object with an event and a session.</summary>
    public static ClaudeHookMessage? Parse(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            var hookEvent = Text(root, "event");
            var session = Text(root, "session_id");
            if (string.IsNullOrEmpty(hookEvent) || string.IsNullOrEmpty(session)) return null;
            return new(hookEvent, session, Text(root, "cwd"), Text(root, "tool"), Text(root, "notification_type"), Text(root, "message"),
                Text(root, "question"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

/// <summary>
/// ~/.claude/sessions/&lt;pid&gt;.json as Claude Code 2.1 writes it (design 3h): not an official format, so every field
/// but the PID and the session is optional. <paramref name="Status"/> is busy, idle or waiting.
/// </summary>
public sealed record ClaudeSessionFile(
    int Pid,
    string SessionId,
    string? Cwd,
    string? Name,
    bool DerivedName,
    string? Entrypoint,
    string? Status,
    string? WaitingFor)
{
    public static ClaudeSessionFile? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("pid", out var pid) || !pid.TryGetInt32(out var id)) return null;
            var session = ClaudeHookMessage.Text(root, "sessionId");
            if (string.IsNullOrEmpty(session)) return null;
            return new(id, session, ClaudeHookMessage.Text(root, "cwd"), ClaudeHookMessage.Text(root, "name"),
                ClaudeHookMessage.Text(root, "nameSource") == "derived", ClaudeHookMessage.Text(root, "entrypoint"),
                ClaudeHookMessage.Text(root, "status"), ClaudeHookMessage.Text(root, "waitingFor"));
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>A live session as Gili tracks it, only in memory (PRD 8.2).</summary>
public sealed class ClaudeSession(string id)
{
    public string Id { get; } = id;
    public int? Pid { get; internal set; }
    public string? Cwd { get; internal set; }
    public ClaudeApp App { get; internal set; }
    public ClaudeStatus Status { get; internal set; }
    public ClaudeNeed? Need { get; internal set; }

    /// <summary>The question's first line, or the permission message.</summary>
    public string? Snippet { get; internal set; }

    /// <summary>When the running prompt was sent.</summary>
    public DateTimeOffset? PromptAt { get; internal set; }

    /// <summary>How long the last finished prompt took, waiting for answers included.</summary>
    public TimeSpan? DoneAfter { get; internal set; }

    /// <summary>Until when the slot shows this session's Done (4 s after Stop).</summary>
    public DateTimeOffset? DoneUntil { get; internal set; }

    public DateTimeOffset? IdleSince { get; internal set; }

    /// <summary>When Claude last stopped after a prompt.</summary>
    public DateTimeOffset? FinishedAt { get; internal set; }

    /// <summary>Finished while its window was in the background and not looked at since (F-CLD-04).</summary>
    public bool Unseen { get; internal set; }

    /// <summary>Numbers each wait for an answer, so one question never opens its card twice (F-CLD-03); 0 before any.</summary>
    public long WaitId { get; internal set; }

    /// <summary>The terminal's name (Windows Terminal, wmux), from the program that owns the session's window.</summary>
    public string? Host { get; internal set; }

    /// <summary>The slot shows this session's Done right now.</summary>
    public bool ShowsDone => Status == ClaudeStatus.Done && DoneUntil is not null;

    internal string? Title { get; set; }

    /// <summary>SessionEnd came or the process died while its Done still shows; it goes once the 4 s are over.</summary>
    internal bool Ended { get; set; }

    /// <summary>The session file went idle; Esc unless Stop arrives first (design 3h).</summary>
    internal DateTimeOffset? IdleWait { get; set; }

    /// <summary>The need came from the session file, so its next busy clears it; a hook's need waits for PostToolUse.</summary>
    internal bool NeedFromFile { get; set; }

    /// <summary>The session title from Claude Desktop, else the folder's name (PRD 6.3 "Nama sesi").</summary>
    public string Name => Title ?? Folder ?? "Claude";

    /// <summary>The working folder's own name (top-taskbar).</summary>
    public string? Folder => string.IsNullOrEmpty(Cwd) ? null : Path.GetFileName(Path.TrimEndingDirectorySeparator(Cwd));
}
