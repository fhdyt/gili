using System.IO;
using System.Windows.Threading;
using Gili.Core;
using Gili.Core.Claude;
using Gili.Core.Diagnostics;
using Gili.Platform.Claude;

namespace Gili.App.Island;

/// <summary>
/// Claude Code sessions (F-CLD-01, F-CLD-02, design 3h): while Show Claude Code sessions is on, Gili's hooks are in
/// ~/.claude/settings.json, the pipe listens and the session files are watched; switching it off takes the hooks out and
/// stops everything. Session data stays in memory (PRD 8.2). Changes reach the UI at most 4 times a second.
/// </summary>
public sealed class ClaudeService : IDisposable
{
    public const string HelperName = "Gili.ClaudeHook.exe";

    private static readonly string ClaudeFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
    private static readonly string SettingsPath = Path.Combine(ClaudeFolder, "settings.json");

    private readonly Dispatcher _dispatcher;
    private readonly FileLog _log;
    private readonly InstallKind _kind;
    private readonly DispatcherTimer _deadline = new();
    private readonly DispatcherTimer _alive = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly DispatcherTimer _second = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _throttle = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private ClaudePipeServer? _pipe;
    private ClaudeSessionFiles? _files;
    private bool _on;
    private bool _pending;
    private bool _justEnabled;
    private DateTimeOffset? _lastEvent;
    private string? _problem;
    private ClaudeBoard? _demo;
    private ClaudeCards? _demoCards;
    // Each session process's window, found once; the PIDs whose terminal name was looked for.
    private readonly Dictionary<int, SessionWindow?> _windows = [];
    private readonly HashSet<int> _hostTried = [];

    public ClaudeService(Dispatcher dispatcher, SettingsHub settings, FileLog log, InstallKind kind, IClaudeWindowStore? windows)
    {
        _dispatcher = dispatcher;
        _log = log;
        _kind = kind;
        _usage = new ClaudeUsage(windows, () => DateTimeOffset.Now);
        _usage.Changed += OnUsageChanged;
        _reset.Tick += (_, _) =>
        {
            _reset.Stop();
            if (Usage.Tick()) UsageEnded?.Invoke();
            ScheduleReset();
        };
        _real = new ClaudeBoard(() => DateTimeOffset.Now, IsFront);
        _real.Changed += OnBoardChanged;
        _deadline.Tick += (_, _) =>
        {
            _deadline.Stop();
            _real.Tick();
            _demo?.Tick();
            // A timer that fires a hair early changes nothing and raises nothing: it must be set again all the same.
            ScheduleDeadline();
        };
        _alive.Tick += (_, _) => _real.RemoveDead(ClaudeSessionFiles.IsAlive);
        _second.Tick += (_, _) => Raise();
        _throttle.Tick += (_, _) =>
        {
            _throttle.Stop();
            if (!_pending) return;
            _pending = false;
            Changed?.Invoke();
        };
        Apply(settings.Current.ClaudeSessions, startup: true);
        settings.Changed += s => Apply(s.ClaudeSessions, startup: false);
    }

    private readonly ClaudeBoard _real;
    private readonly ClaudeCards _cards = new();
    private readonly ClaudeUsage _usage;
    private readonly DispatcherTimer _reset = new();
    private ClaudeUsage? _demoUsage;

    /// <summary>The Claude 5-hour window (F-CLD-08): the demo's while it plays.</summary>
    public ClaudeUsage Usage => _demoUsage ?? _usage;

    /// <summary>Show Claude Code sessions is on.</summary>
    public bool IsOn => _on;

    /// <summary>The demo's sessions and window are on screen.</summary>
    public bool InDemo => _demo is not null;

    /// <summary>The 5-hour window has just ended: the Claude usage reset card rings (PRD 6.3).</summary>
    public event Action? UsageEnded;

    /// <summary>The sessions on show: the demo's while it plays (F-BAR-09), else the real ones.</summary>
    public ClaudeBoard Board => _demo ?? _real;

    /// <summary>Which wait the Needs input card stands for (F-CLD-03); the demo has its own.</summary>
    public ClaudeCards Cards => _demoCards ?? _cards;

    /// <summary>The sessions or the running time changed.</summary>
    public event Action? Changed;

    /// <summary>The Settings status line changed.</summary>
    public event Action? StatusChanged;

    /// <summary>The status line under the switch (PRD 9.4, 11); null while the switch is off.</summary>
    public string? Status(DateTimeOffset now)
    {
        if (!_on) return null;
        if (_problem is not null) return _problem;
        if (_lastEvent is { } last) return "Connected · last event " + Ago(now - last);
        return _justEnabled ? "Applies to Claude sessions started from now on" : "Waiting for the first Claude session";
    }

    public void Dispose()
    {
        Stop();
        _throttle.Stop();
    }

    private void Apply(bool on, bool startup)
    {
        // At start the hooks are written again too, so a moved or updated Gili points them at its own helper.
        if (on == _on && !startup) return;
        _on = on;
        if (on)
        {
            _justEnabled = !startup;
            InstallHooks();
            Start();
        }
        else
        {
            Stop();
            if (!startup) Edit(text => ClaudeHookConfig.Uninstall(text ?? ""), onlyIfExists: true);
        }
        StatusChanged?.Invoke();
    }

    private void InstallHooks()
    {
        if (HelperPath() is not { } helper)
        {
            _problem = "Hook helper not found · Reinstall";
            _log.Write($"{HelperName} was not found, so the Claude hooks were not written");
            return;
        }
        Edit(text => ClaudeHookConfig.Install(text, helper), onlyIfExists: false);
    }

    /// <summary>
    /// The installed copy's helper sits beside Gili.exe; the Store copy runs it through its app execution alias, since
    /// its WindowsApps path changes with every update (PRD 9.4). A dev build without its own helper uses the installed one.
    /// </summary>
    private string? HelperPath()
    {
        if (_kind == InstallKind.Store)
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "gili-claude-hook.exe");
        var own = Path.Combine(AppContext.BaseDirectory, HelperName);
        if (File.Exists(own)) return own;
        var installed = Path.Combine(AppPaths.InstallDirectory, HelperName);
        return _kind == InstallKind.Development && File.Exists(installed) ? installed : null;
    }

    /// <summary>
    /// Changes Claude's settings.json through a backup (settings.json.gili-backup) and a temp file. A file Gili cannot
    /// read is never written (F-CLD-01, PRD 11).
    /// </summary>
    private void Edit(Func<string?, string> change, bool onlyIfExists)
    {
        try
        {
            var exists = File.Exists(SettingsPath);
            if (!exists && onlyIfExists) return;
            var text = exists ? File.ReadAllText(SettingsPath) : null;
            string next;
            try
            {
                next = change(text);
            }
            catch (ClaudeSettingsException ex)
            {
                _problem = "Can't read Claude settings";
                _log.Write("~/.claude/settings.json was left as it is", ex);
                return;
            }
            _problem = null;
            if (next == text) return;
            Directory.CreateDirectory(ClaudeFolder);
            if (exists) File.Copy(SettingsPath, SettingsPath + ".gili-backup", overwrite: true);
            var temp = SettingsPath + ".gili-tmp";
            File.WriteAllText(temp, next);
            File.Move(temp, SettingsPath, overwrite: true);
            _log.Write(_on ? "Claude hooks written" : "Claude hooks removed");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _problem = "Can't save Claude settings";
            _log.Write("Could not change ~/.claude/settings.json", ex);
        }
    }

    private void Start()
    {
        if (_pipe is not null) return;
        _pipe = new ClaudePipeServer();
        _pipe.LineReceived += line =>
        {
            if (ClaudeHookMessage.Parse(line) is not { } message) return;
            _dispatcher.InvokeAsync(() => Guard(() =>
            {
                if (!_on) return;
                var first = _lastEvent is null;
                _lastEvent = DateTimeOffset.Now;
                _real.Apply(message);
                // The first prompt starts the 5-hour window; a turn ended on a rate limit marks it reached (design 3j).
                if (message.Event == "UserPromptSubmit") _usage.Prompted();
                else if (message is { Event: "StopFailure", Error: "rate_limit" }) _usage.LimitReached(message.Message);
                if (first) StatusChanged?.Invoke();
            }));
        };
        _pipe.Failed += ex => _log.Write("Could not open the Claude pipe", ex);
        _pipe.Start();
        _files = new ClaudeSessionFiles(ClaudeFolder);
        _files.Read += texts => _dispatcher.InvokeAsync(() => Guard(() =>
        {
            if (!_on) return;
            foreach (var text in texts)
            {
                if (ClaudeSessionFile.Parse(text) is { } file) _real.Apply(file);
            }
            _real.RemoveDead(ClaudeSessionFiles.IsAlive);
            NameHosts();
        }));
        _files.Start();
        _usage.Load();
    }

    private void Stop()
    {
        _pipe?.Dispose();
        _pipe = null;
        _files?.Dispose();
        _files = null;
        _deadline.Stop();
        _alive.Stop();
        _second.Stop();
        _lastEvent = null;
        _real.Clear();
        _windows.Clear();
        _hostTried.Clear();
        _reset.Stop();
    }

    /// <summary>
    /// The reset card's Open Claude: Claude Desktop's window when a Desktop session lives, else the latest terminal
    /// session's; false when there is no session to show, so the island opens the Claude page instead.
    /// </summary>
    public bool OpenClaude()
    {
        var sessions = Board.Sessions;
        var session = sessions.LastOrDefault(s => s.App == ClaudeApp.Desktop) ?? sessions.LastOrDefault();
        if (session is not { Pid: > 0 } || FindWindow(session.Pid!.Value, fresh: true) is not { } window) return false;
        SessionWindows.BringToFront(window);
        return true;
    }

    /// <summary>The clock jumped or the laptop woke: the reset is due at its own time again.</summary>
    public void RescheduleUsage() => ScheduleReset();

    /// <summary>The demo's own 5-hour window, never stored (F-BAR-09).</summary>
    public void ApplyDemo(ClaudeUsageWindow window) => _demoUsage?.ShowDemo(window);

    private void OnUsageChanged()
    {
        ScheduleReset();
        Raise();
    }

    private void ScheduleReset()
    {
        _reset.Stop();
        if (Usage.NextDeadline is not { } at || (!_on && _demoUsage is null)) return;
        _reset.Interval = TimeSpan.FromMilliseconds(Math.Clamp((at - DateTimeOffset.Now).TotalMilliseconds, 1, int.MaxValue));
        _reset.Start();
    }

    /// <summary>
    /// Brings the session's window forward (F-CLD-06) and counts its finish as seen (F-CLD-04). A demo session has no
    /// window; it is only marked seen.
    /// </summary>
    public void Focus(string id)
    {
        var board = Board;
        if (board.Sessions.FirstOrDefault(s => s.Id == id) is { Pid: > 0 } session && FindWindow(session.Pid!.Value, fresh: true) is { } window)
            SessionWindows.BringToFront(window);
        board.MarkSeen(id);
    }

    /// <summary>A window came forward: a session that finished unseen behind it is seen now (F-CLD-04).</summary>
    public void OnForeground(nint hwnd)
    {
        if (!_real.Sessions.Any(s => s.Unseen)) return;
        var pid = SessionWindows.PidOf(hwnd);
        if (pid == 0) return;
        _real.MarkSeen(s => s.Pid is > 0 && FindWindow(s.Pid.Value, fresh: false)?.Pid == pid);
    }

    /// <summary>The demo's own sessions replace the real ones on screen until it ends (F-BAR-09).</summary>
    public void BeginDemo()
    {
        if (_demo is not null) return;
        _demo = new ClaudeBoard(() => DateTimeOffset.Now, _ => false);
        _demoCards = new ClaudeCards();
        _demoUsage = new ClaudeUsage(null, () => DateTimeOffset.Now);
        _demoUsage.Changed += OnUsageChanged;
        _demo.Changed += OnBoardChanged;
        OnBoardChanged();
    }

    public void ApplyDemo(ClaudeHookMessage message) => _demo?.Apply(message);

    public void ApplyDemo(ClaudeSessionFile file) => _demo?.Apply(file);

    public void EndDemo()
    {
        if (_demo is null) return;
        _demo.Changed -= OnBoardChanged;
        _demo = null;
        _demoCards = null;
        _demoUsage!.Changed -= OnUsageChanged;
        _demoUsage = null;
        ScheduleReset();
        OnBoardChanged();
    }

    /// <summary>InvokeAsync keeps an exception in its task, where nobody sees it; the log does.</summary>
    private void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _log.Write("A Claude event could not be applied", ex);
        }
    }

    /// <summary>Whether the session's window is the foreground one when Claude stops.</summary>
    private bool IsFront(ClaudeSession session) =>
        session.Pid is > 0 && FindWindow(session.Pid.Value, fresh: true) is { } window && window.Pid == SessionWindows.ForegroundPid();

    /// <summary>A session's window, from the cache unless asked fresh (a click, a Stop) or the cached one is gone.</summary>
    private SessionWindow? FindWindow(int pid, bool fresh)
    {
        if (!fresh && _windows.TryGetValue(pid, out var known) && (known is null || SessionWindows.PidOf(known.Handle) == known.Pid)) return known;
        try
        {
            return _windows[pid] = SessionWindows.Find(pid);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException)
        {
            _log.Write("Could not find a Claude session's window", ex);
            return _windows[pid] = null;
        }
    }

    /// <summary>A terminal session's app name (Terminal, wmux), looked up once when its PID is first known.</summary>
    private void NameHosts()
    {
        foreach (var s in _real.Sessions.Where(s => s is { App: ClaudeApp.Terminal, Pid: > 0 } && _hostTried.Add(s.Pid!.Value)).ToList())
            _real.SetHost(s.Id, FindWindow(s.Pid!.Value, fresh: true)?.Program);
    }

    private void OnBoardChanged()
    {
        var sessions = Board.Sessions;
        _alive.IsEnabled = _real.Sessions.Count > 0;
        // The running time ticks only while one session works alone in the slot (PRD 8.1).
        _second.IsEnabled = sessions.Count(s => s.Status == ClaudeStatus.Working) == 1;
        ScheduleDeadline();
        Raise();
    }

    /// <summary>The next Done to end or Esc to show, on either board.</summary>
    private void ScheduleDeadline()
    {
        _deadline.Stop();
        var next = _real.NextDeadline;
        if (_demo?.NextDeadline is { } demo && (next is null || demo < next)) next = demo;
        if (next is null) return;
        _deadline.Interval = TimeSpan.FromMilliseconds(Math.Max(1, (next.Value - DateTimeOffset.Now).TotalMilliseconds));
        _deadline.Start();
    }

    /// <summary>Right away, then at most once per 250 ms.</summary>
    private void Raise()
    {
        if (_throttle.IsEnabled)
        {
            _pending = true;
            return;
        }
        Changed?.Invoke();
        _throttle.Start();
    }

    private static string Ago(TimeSpan time) =>
        time < TimeSpan.FromMinutes(1) ? "just now"
        : time < TimeSpan.FromHours(1) ? $"{(int)time.TotalMinutes} min ago"
        : $"{(int)time.TotalHours} h ago";
}
