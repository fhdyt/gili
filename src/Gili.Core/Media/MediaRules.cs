using System.Globalization;

namespace Gili.Core.Media;

public static class MediaRules
{
    public const int MusicSlotLength = 36;
    public const int VideoSlotLength = 32;
    private const string VlcSuffix = " - VLC media player";

    private static readonly string[] VideoApps = ["vlc", "Microsoft.ZuneVideo", "mpc-hc", "PotPlayer", "mpv"];

    private static readonly (string Match, string Name)[] KnownApps =
    [
        ("Microsoft.ZuneMusic", "Media Player"),
        ("Microsoft.ZuneVideo", "Movies & TV"),
        ("spotify", "Spotify"),
        ("chrome", "Google Chrome"),
        ("msedge", "Microsoft Edge"),
        ("firefox", "Firefox"),
        ("vlc", "VLC"),
    ];

    /// <summary>Music or video from the session's playback type, else from apps known to play video (PRD 9.2).</summary>
    public static MediaKind Classify(PlaybackType type, string appId) => type switch
    {
        PlaybackType.Video => MediaKind.Video,
        PlaybackType.Music => MediaKind.Music,
        _ => VideoApps.Any(app => appId.Contains(app, StringComparison.OrdinalIgnoreCase)) ? MediaKind.Video : MediaKind.Music,
    };

    /// <summary>The source app's name from its AppUserModelID ("Spotify.exe", "Chrome", "Package_hash!App").</summary>
    public static string AppName(string appId)
    {
        foreach (var (match, name) in KnownApps)
        {
            if (appId.Contains(match, StringComparison.OrdinalIgnoreCase)) return name;
        }
        var bang = appId.LastIndexOf('!');
        var fallback = bang >= 0 ? appId[(bang + 1)..] : Path.GetFileNameWithoutExtension(appId);
        return fallback.Length == 0 ? appId : char.ToUpperInvariant(fallback[0]) + fallback[1..];
    }

    /// <summary>Colour of the music level bars: Spotify's green for Spotify, the accent green for everything else.</summary>
    public static string LevelColor(string appId) =>
        appId.Contains("spotify", StringComparison.OrdinalIgnoreCase) ? "#1DB954" : "#30D158";

    /// <summary>VLC 3 has no media session; "file.mkv - VLC media player" gives the video title (PRD 12).</summary>
    public static string? VlcTitle(string windowTitle) =>
        windowTitle.EndsWith(VlcSuffix, StringComparison.Ordinal) ? windowTitle[..^VlcSuffix.Length] : null;

    public static string SlotTitle(MediaState media) => media.Kind == MediaKind.Video
        ? Truncate(media.Title, VideoSlotLength)
        : Truncate(string.IsNullOrWhiteSpace(media.Artist) ? media.Title : $"{media.Title} – {media.Artist}", MusicSlotLength);

    public static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..(max - 1)].TrimEnd() + "…";

    public static string Time(TimeSpan time)
    {
        var t = time < TimeSpan.Zero ? TimeSpan.Zero : time;
        return t.TotalHours >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{t.Minutes}:{t.Seconds:00}");
    }

    public static string Remaining(TimeSpan position, TimeSpan duration) => "-" + Time(duration - position);

    public static double Progress(TimeSpan position, TimeSpan duration) =>
        duration > TimeSpan.Zero ? Math.Clamp(position / duration, 0, 1) : 0;
}
