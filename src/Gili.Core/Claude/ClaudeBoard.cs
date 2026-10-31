namespace Gili.Core.Claude;

/// <summary>
/// Every live Claude Code session and its status, fed by the hooks and the session files (PRD 6.3 "Sesi Claude",
/// design 3h). Hooks give the meaning (prompt, question, plan, stop); the files give the title, the PID, sessions that
/// started before Gili, permissions Claude Desktop handles itself (status waiting) and Esc (status idle without Stop).
/// </summary>
public sealed class ClaudeBoard(Func<DateTimeOffset> now)
{
    public static readonly TimeSpan DoneShows = TimeSpan.FromSeconds(4);

    /// <summary>How long an idle session file waits for the Stop hook before it counts as Esc.</summary>
    public static readonly TimeSpan StopGrace = TimeSpan.FromSeconds(1.5);

    private readonly List<ClaudeSession> _sessions = [];

    public IReadOnlyList<ClaudeSession> Sessions => _sessions;

    public event Action? Changed;

    /// <summary>When <see cref="Tick"/> must run for a Done to end or an Esc to show; null when nothing waits.</summary>
    public DateTimeOffset? NextDeadline
    {
        get
        {
            DateTimeOffset? next = null;
            foreach (var s in _sessions)
            {
                foreach (var at in new[] { s.DoneUntil, s.IdleWait })
                {
                    if (at is { } t && (next is null || t < next)) next = t;
                }
            }
            return next;
        }
    }

    public void Apply(ClaudeHookMessage message) => Change(() =>
    {
        if (message.Event == "SessionEnd")
        {
            _sessions.RemoveAll(s => s.Id == message.SessionId);
            return;
        }
        var s = Get(message.SessionId, message.Cwd);
        switch (message.Event)
        {
            case "UserPromptSubmit":
                s.Status = ClaudeStatus.Working;
                s.PromptAt = now();
                ClearWait(s);
                break;
            case "PreToolUse" when message.Tool == "AskUserQuestion":
                Need(s, ClaudeNeed.Question, message.Question);
                break;
            case "PreToolUse" when message.Tool == "ExitPlanMode":
                Need(s, ClaudeNeed.Plan, null);
                break;
            case "Notification" when message.NotificationType == "permission_prompt":
                Need(s, ClaudeNeed.Permission, message.Message);
                break;
            case "PostToolUse" when s.Status == ClaudeStatus.NeedsInput:
                s.Status = ClaudeStatus.Working;
                ClearWait(s);
                break;
            case "Stop" when s.Status is ClaudeStatus.Working or ClaudeStatus.NeedsInput:
                s.Status = ClaudeStatus.Done;
                s.DoneAfter = s.PromptAt is { } prompt ? now() - prompt : null;
                s.DoneUntil = now() + DoneShows;
                ClearWait(s);
                break;
        }
    });

    public void Apply(ClaudeSessionFile file) => Change(() =>
    {
        var s = Get(file.SessionId, file.Cwd);
        s.Pid = file.Pid;
        s.Title = file.DerivedName || string.IsNullOrWhiteSpace(file.Name) ? null : file.Name;
        s.App = file.Entrypoint == "claude-desktop" ? ClaudeApp.Desktop : ClaudeApp.Terminal;
        switch (file.Status)
        {
            case "busy":
                s.IdleWait = null;
                if (s.Status == ClaudeStatus.Idle)
                {
                    s.Status = ClaudeStatus.Working;
                    s.PromptAt = now();
                }
                else if (s.Status == ClaudeStatus.NeedsInput && s.NeedFromFile)
                {
                    s.Status = ClaudeStatus.Working;
                    ClearWait(s);
                }
                break;
            case "waiting" when s.Status == ClaudeStatus.Working && file.WaitingFor != "dialog open":
                Need(s, file.WaitingFor == "input needed" ? ClaudeNeed.Question
                    : file.WaitingFor?.Contains("plan", StringComparison.OrdinalIgnoreCase) == true ? ClaudeNeed.Plan
                    : ClaudeNeed.Permission, null);
                s.NeedFromFile = true;
                break;
            case "idle" when s.Status is ClaudeStatus.Working or ClaudeStatus.NeedsInput:
                s.IdleWait ??= now() + StopGrace;
                break;
        }
    });

    /// <summary>Ends a Done after 4 s, and turns a session whose file went idle without Stop into Idle (Esc).</summary>
    public void Tick() => Change(() =>
    {
        var t = now();
        foreach (var s in _sessions)
        {
            if (s.DoneUntil is { } done && done <= t)
            {
                s.Status = ClaudeStatus.Idle;
                s.DoneUntil = null;
                s.IdleSince = done;
            }
            if (s.IdleWait is { } wait && wait <= t)
            {
                s.Status = ClaudeStatus.Idle;
                s.IdleSince = t;
                ClearWait(s);
            }
        }
    });

    /// <summary>Forgets every session, when the integration is switched off.</summary>
    public void Clear() => Change(_sessions.Clear);

    /// <summary>Drops sessions whose process is gone, e.g. Claude Desktop closed by force without SessionEnd.</summary>
    public void RemoveDead(Func<int, bool> isAlive) => Change(() => _sessions.RemoveAll(s => s.Pid is { } pid && !isAlive(pid)));

    private ClaudeSession Get(string id, string? cwd)
    {
        var s = _sessions.Find(x => x.Id == id);
        if (s is null)
        {
            s = new ClaudeSession(id) { IdleSince = now() };
            _sessions.Add(s);
        }
        if (!string.IsNullOrEmpty(cwd)) s.Cwd = cwd;
        return s;
    }

    private static void Need(ClaudeSession s, ClaudeNeed need, string? snippet)
    {
        s.Status = ClaudeStatus.NeedsInput;
        s.Need = need;
        s.Snippet = snippet;
        s.NeedFromFile = false;
        s.IdleWait = null;
        s.DoneUntil = null;
    }

    private static void ClearWait(ClaudeSession s)
    {
        s.Need = null;
        s.Snippet = null;
        s.NeedFromFile = false;
        s.IdleWait = null;
        if (s.Status != ClaudeStatus.Done) s.DoneUntil = null;
    }

    private void Change(Action action)
    {
        var before = Signature();
        action();
        if (Signature() != before) Changed?.Invoke();
    }

    private string Signature() => string.Join("|", _sessions.Select(s =>
        $"{s.Id},{s.Status},{s.Need},{s.Snippet},{s.Name},{s.App},{s.Pid},{s.PromptAt?.Ticks},{s.DoneUntil?.Ticks},{s.IdleWait?.Ticks}"));
}
