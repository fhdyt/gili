using TopBar.Core.Media;
using Windows.Media;
using Windows.Media.Control;
using Windows.Storage.Streams;
using Session = Windows.Media.Control.GlobalSystemMediaTransportControlsSession;

namespace TopBar.Platform.Media;

/// <summary>
/// Follows Windows' media sessions (Spotify, browsers, Media Player…) and reports the one to show (PRD 9.2, 12):
/// a playing session before a paused one, and among those the one whose track or playback changed last.
/// </summary>
public sealed class MediaSessionMonitor : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, Session> _hooked = [];
    private readonly Dictionary<string, DateTimeOffset> _lastChange = [];
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private Session? _current;
    private (string Key, byte[] Cover)? _cover;
    private volatile bool _disposed;

    /// <summary>The session to show, or null when there is none. Raised on a background thread.</summary>
    public event Action<MediaState?>? Changed;

    /// <summary>Raised on a background thread when Windows refuses a request; the bar keeps running.</summary>
    public event Action<Exception>? Failed;

    public async Task StartAsync()
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _manager.SessionsChanged += (_, _) => _ = RefreshAsync(null);
        await RefreshAsync(null);
    }

    public Task TogglePlayPauseAsync() => RunAsync(s => s.TryTogglePlayPauseAsync());

    public Task NextAsync() => RunAsync(s => s.TrySkipNextAsync());

    public Task PreviousAsync() => RunAsync(s => s.TrySkipPreviousAsync());

    public Task SeekAsync(TimeSpan position) =>
        RunAsync(s => s.TryChangePlaybackPositionAsync((s.GetTimelineProperties().StartTime + position).Ticks));

    public void Dispose() => _disposed = true;

    private async Task RefreshAsync(Session? changed)
    {
        if (_disposed || _manager is null) return;
        await _gate.WaitAsync();
        try
        {
            if (changed is not null) _lastChange[changed.SourceAppUserModelId] = DateTimeOffset.UtcNow;
            var sessions = _manager.GetSessions();
            foreach (var session in sessions) Hook(session);
            _current = sessions
                .OrderByDescending(s => s.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                .ThenByDescending(s => _lastChange.GetValueOrDefault(s.SourceAppUserModelId))
                .FirstOrDefault();
            var state = _current is null ? null : await ReadAsync(_current);
            if (!_disposed) Changed?.Invoke(state);
        }
        catch (Exception ex)
        {
            Failed?.Invoke(ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void Hook(Session session)
    {
        var id = session.SourceAppUserModelId;
        if (_hooked.TryGetValue(id, out var known))
        {
            if (ReferenceEquals(known, session)) return;
            Unhook(known);
        }
        _hooked[id] = session;
        session.MediaPropertiesChanged += OnMediaPropertiesChanged;
        session.PlaybackInfoChanged += OnPlaybackInfoChanged;
        session.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
    }

    private void Unhook(Session session)
    {
        session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
        session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        session.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
    }

    private void OnMediaPropertiesChanged(Session sender, MediaPropertiesChangedEventArgs args) => _ = RefreshAsync(sender);

    private void OnPlaybackInfoChanged(Session sender, PlaybackInfoChangedEventArgs args) => _ = RefreshAsync(sender);

    // Some apps report the position every second; that alone must not make a background session "most recent".
    private void OnTimelinePropertiesChanged(Session sender, TimelinePropertiesChangedEventArgs args) => _ = RefreshAsync(null);

    private async Task<MediaState?> ReadAsync(Session session)
    {
        var properties = await session.TryGetMediaPropertiesAsync();
        if (properties is null || string.IsNullOrWhiteSpace(properties.Title)) return null;
        var playback = session.GetPlaybackInfo();
        var timeline = session.GetTimelineProperties();
        var appId = session.SourceAppUserModelId;
        var type = (properties.PlaybackType ?? playback.PlaybackType) switch
        {
            MediaPlaybackType.Music => PlaybackType.Music,
            MediaPlaybackType.Video => PlaybackType.Video,
            MediaPlaybackType.Image => PlaybackType.Image,
            _ => PlaybackType.Unknown,
        };
        // Some apps never stamp the timeline; extrapolating from year 1 would jump straight to the end.
        var updatedAt = timeline.LastUpdatedTime.Year < 2000 ? DateTimeOffset.UtcNow : timeline.LastUpdatedTime;
        return new MediaState(
            properties.Title,
            properties.Artist ?? "",
            appId,
            MediaRules.Classify(type, appId),
            playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
            timeline.Position - timeline.StartTime,
            timeline.EndTime - timeline.StartTime,
            updatedAt,
            playback.Controls.IsPlaybackPositionEnabled,
            playback.Controls.IsNextEnabled,
            await ReadCoverAsync(properties, appId));
    }

    /// <summary>Cover art bytes, read once per track. Apps often attach the thumbnail a moment after the title, so a miss is not cached.</summary>
    private async Task<byte[]?> ReadCoverAsync(GlobalSystemMediaTransportControlsSessionMediaProperties properties, string appId)
    {
        var key = $"{appId}\n{properties.Title}\n{properties.Artist}";
        if (_cover is { } cached && cached.Key == key) return cached.Cover;
        if (properties.Thumbnail is null) return null;
        using var stream = await properties.Thumbnail.OpenReadAsync();
        using var reader = new DataReader(stream);
        var size = (uint)stream.Size;
        await reader.LoadAsync(size);
        var bytes = new byte[size];
        reader.ReadBytes(bytes);
        _cover = (key, bytes);
        return bytes;
    }

    private async Task RunAsync(Func<Session, Windows.Foundation.IAsyncOperation<bool>> command)
    {
        if (_current is not { } session) return;
        try
        {
            await command(session);
        }
        catch (Exception ex)
        {
            Failed?.Invoke(ex);
        }
    }
}
