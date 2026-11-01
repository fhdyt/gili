using System.Windows.Threading;
using Gili.Core.Lyrics;
using Gili.Core.Media;
using Gili.Core.Settings;
using Gili.Platform.Media;

namespace Gili.App.Island;

/// <summary>
/// Lyrics of the playing song (F-ISL-26, PRD 6.3 "Lirik lagu"): looked up on LRCLIB once per song while Show lyrics is on,
/// kept in memory for the last 20 songs, and followed 4× a second only while synced lyrics play.
/// </summary>
public sealed class LyricsService : IDisposable
{
    private readonly MediaService _media;
    private readonly Func<AppSettings> _settings;
    private readonly LyricsCache _cache = new();
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private string? _key;
    private CancellationTokenSource? _search;
    private IReadOnlyList<LyricLine>? _demo;

    public LyricsService(MediaService media, Func<AppSettings> settings)
    {
        _media = media;
        _settings = settings;
        _media.Updated += Sync;
        _tick.Tick += (_, _) => FollowPosition();
    }

    public LyricsStatus Status { get; private set; }

    public IReadOnlyList<LyricLine>? Lines { get; private set; }

    /// <summary>The line being sung; −1 before the first.</summary>
    public int Index { get; private set; } = -1;

    /// <summary>The status or the lines changed (a new song, a search that finished, Show lyrics switched).</summary>
    public event Action? Changed;

    /// <summary>Another line started, or a seek moved to one.</summary>
    public event Action? LineChanged;

    /// <summary>Lyrics are on and the media is music, so the Media page shows the lyrics block.</summary>
    public bool Shown => Status != LyricsStatus.Off;

    /// <summary>The slot's line while music plays and a line is sung; null otherwise, so the slot shows the title.</summary>
    public string? SlotLine =>
        Status == LyricsStatus.Synced && Lines is { } lines && _media.Current is { IsPlaying: true, Kind: MediaKind.Music }
            ? LyricsView.Slot(lines, Index)
            : null;

    /// <summary>The demo's own lines for its song, so it never reaches LRCLIB; null ends them (F-BAR-09).</summary>
    public void SetDemo(IReadOnlyList<LyricLine>? lines)
    {
        _demo = lines;
        _key = null;
        Sync();
    }

    /// <summary>A new song, Show lyrics switched, or the second tick: look up what is needed and follow the position.</summary>
    public void Sync()
    {
        var media = _media.Current;
        var key = (_demo is not null || _settings().ShowLyrics) && media is { Kind: MediaKind.Music } ? $"{media.AppId}\n{media.Title}\n{media.Artist}" : null;
        if (key != _key)
        {
            _key = key;
            _search?.Cancel();
            Index = -1;
            if (key is null) Set(LyricsStatus.Off, null);
            else if (_demo is not null) Set(LyricsStatus.Synced, _demo);
            else if (_cache.TryGet(key, out var status, out var lines)) Set(status, lines);
            else
            {
                Set(LyricsStatus.Searching, null);
                _ = SearchAsync(key, media!);
            }
        }
        if (Status == LyricsStatus.Synced && media?.IsPlaying == true) _tick.Start();
        else _tick.Stop();
        FollowPosition();
    }

    private async Task SearchAsync(string key, MediaState media)
    {
        var cancel = _search = new CancellationTokenSource();
        var (status, lines) = await LrclibClient.FindAsync(media.Title, media.Artist, media.Album, media.Duration, cancel.Token);
        if (cancel.IsCancellationRequested || key != _key) return;
        _cache.Add(key, status, lines);
        Set(status, lines);
        Sync();
    }

    private void Set(LyricsStatus status, IReadOnlyList<LyricLine>? lines)
    {
        Status = status;
        Lines = lines;
        Changed?.Invoke();
    }

    private void FollowPosition()
    {
        var index = Lines is { } lines && _media.Current is { } media ? LyricsView.Index(lines, media.PositionAt(DateTimeOffset.Now)) : -1;
        if (index == Index) return;
        Index = index;
        LineChanged?.Invoke();
    }

    public void Dispose()
    {
        _tick.Stop();
        _search?.Cancel();
    }
}
