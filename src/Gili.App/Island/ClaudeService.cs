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

    public ClaudeService(Dispatcher dispatcher, SettingsHub settings, FileLog log, InstallKind kind)
    {
        _dispatcher = dispatcher;
        _log = log;
        _kind = kind;
        Board.Changed += OnBoardChanged;
        _deadline.Tick += (_, _) =>
        {
            _deadline.Stop();
            Board.Tick();
        };
        _alive.Tick += (_, _) => Board.RemoveDead(ClaudeSessionFiles.IsAlive);
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

    public ClaudeBoard Board { get; } = new(() => DateTimeOffset.Now);

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
            _dispatcher.InvokeAsync(() =>
            {
                if (!_on) return;
                var first = _lastEvent is null;
                _lastEvent = DateTimeOffset.Now;
                Board.Apply(message);
                if (first) StatusChanged?.Invoke();
            });
        };
        _pipe.Failed += ex => _log.Write("Could not open the Claude pipe", ex);
        _pipe.Start();
        _files = new ClaudeSessionFiles(ClaudeFolder);
        _files.Read += texts => _dispatcher.InvokeAsync(() =>
        {
            if (!_on) return;
            foreach (var text in texts)
            {
                if (ClaudeSessionFile.Parse(text) is { } file) Board.Apply(file);
            }
            Board.RemoveDead(ClaudeSessionFiles.IsAlive);
        });
        _files.Start();
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
        Board.Clear();
    }

    private void OnBoardChanged()
    {
        var sessions = Board.Sessions;
        _alive.IsEnabled = sessions.Count > 0;
        // The running time ticks only while one session works alone in the slot (PRD 8.1).
        _second.IsEnabled = sessions.Count(s => s.Status == ClaudeStatus.Working) == 1;
        _deadline.Stop();
        if (Board.NextDeadline is { } next)
        {
            _deadline.Interval = TimeSpan.FromMilliseconds(Math.Max(1, (next - DateTimeOffset.Now).TotalMilliseconds));
            _deadline.Start();
        }
        Raise();
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
