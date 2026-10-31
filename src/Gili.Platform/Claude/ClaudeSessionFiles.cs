using System.Diagnostics;

namespace Gili.Platform.Claude;

/// <summary>
/// ~/.claude/sessions/*.json, which Claude Code rewrites as a session's status changes (PRD 9.2, design 3h). Changes are
/// read together at most every 250 ms; the texts arrive on a thread-pool thread. While the folder does not exist yet
/// (Claude never ran), it is looked for again every 30 s.
/// </summary>
public sealed class ClaudeSessionFiles : IDisposable
{
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan LookAgain = TimeSpan.FromSeconds(30);

    private readonly string _folder;
    private readonly Timer _timer;
    private FileSystemWatcher? _watcher;

    public ClaudeSessionFiles(string claudeFolder)
    {
        _folder = Path.Combine(claudeFolder, "sessions");
        _timer = new Timer(_ => Scan());
    }

    /// <summary>Every session file's text, read in one pass.</summary>
    public event Action<IReadOnlyList<string>>? Read;

    public void Start() => _timer.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan);

    public void Dispose()
    {
        _timer.Dispose();
        _watcher?.Dispose();
    }

    /// <summary>Whether a session's process still runs; Claude Code's PID, from its session file.</summary>
    public static bool IsAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private void Scan()
    {
        if (!Directory.Exists(_folder))
        {
            _timer.Change(LookAgain, Timeout.InfiniteTimeSpan);
            return;
        }
        Watch();
        var texts = new List<string>();
        foreach (var file in Directory.EnumerateFiles(_folder, "*.json"))
        {
            try
            {
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                texts.Add(reader.ReadToEnd());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Being rewritten or already gone; the next change reads it again.
            }
        }
        Read?.Invoke(texts);
    }

    private void Watch()
    {
        if (_watcher is not null) return;
        _watcher = new FileSystemWatcher(_folder, "*.json")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
        };
        FileSystemEventHandler changed = (_, _) => _timer.Change(Settle, Timeout.InfiniteTimeSpan);
        _watcher.Changed += changed;
        _watcher.Created += changed;
        _watcher.Deleted += changed;
        _watcher.Renamed += (_, _) => _timer.Change(Settle, Timeout.InfiniteTimeSpan);
        _watcher.EnableRaisingEvents = true;
    }
}
