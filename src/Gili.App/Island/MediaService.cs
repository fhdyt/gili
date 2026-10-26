using System.Windows.Threading;
using Gili.Core.Diagnostics;
using Gili.Core.Media;
using Gili.Platform.Media;

namespace Gili.App.Island;

/// <summary>
/// The media the island shows: Windows' media session, or VLC's window title when no session exists (PRD 9.2).
/// Ticks every second while there is media, so progress and the paused grace period stay current.
/// </summary>
public sealed class MediaService : IDisposable
{
    private readonly FileLog _log;
    private readonly MediaSessionMonitor _monitor = new();
    private readonly MediaPresence _presence = new();
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(1) };
    private MediaState? _session;
    private MediaState? _vlc;
    private int _ticks;

    public MediaService(Dispatcher dispatcher, FileLog log)
    {
        _log = log;
        _monitor.Changed += state => dispatcher.InvokeAsync(() =>
        {
            _session = state;
            Publish();
        });
        _monitor.Failed += ex => _log.Write("Media session request failed", ex);
        _tick.Tick += (_, _) => OnTick();
    }

    public MediaState? Current => _session ?? _vlc;

    /// <summary>Whether media holds the compact slot (see MediaPresence).</summary>
    public bool IsActive { get; private set; }

    /// <summary>Raised on the UI thread when the media changes, and once a second while there is media.</summary>
    public event Action? Updated;

    public async Task StartAsync()
    {
        _tick.Start();
        try
        {
            await _monitor.StartAsync();
        }
        catch (Exception ex)
        {
            _log.Write("Media sessions are unavailable", ex);
        }
    }

    public Task TogglePlayPauseAsync() => _monitor.TogglePlayPauseAsync();

    public Task NextAsync() => _monitor.NextAsync();

    public Task PreviousAsync() => _monitor.PreviousAsync();

    public Task SeekAsync(TimeSpan position) => _monitor.SeekAsync(position);

    public void Dispose()
    {
        _tick.Stop();
        _monitor.Dispose();
    }

    private void OnTick()
    {
        // VLC is checked every 2 s, and only while no media session covers it (PRD 9.2).
        if (_session is null && ++_ticks % 2 == 0) ReadVlc();
        if (Current is not null || IsActive) Publish();
    }

    private void ReadVlc()
    {
        var title = VlcWindow.ReadTitle();
        if (title == _vlc?.Title) return;
        _vlc = title is null
            ? null
            : new MediaState(title, "", "vlc.exe", MediaKind.Video, IsPlaying: true, TimeSpan.Zero, TimeSpan.Zero,
                DateTimeOffset.Now, CanSeek: false, CanSkip: false, Cover: null);
        Publish();
    }

    private void Publish()
    {
        IsActive = _presence.IsActive(Current, DateTimeOffset.Now);
        Updated?.Invoke();
    }
}
