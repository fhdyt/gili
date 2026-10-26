namespace Gili.Core.Media;

public enum MediaKind { Music, Video }

/// <summary>The playback type a media session reports to Windows (PRD 9.2).</summary>
public enum PlaybackType { Unknown, Music, Video, Image }

/// <summary>The media session the island shows. Windows reports the position only when it changes, at <paramref name="PositionUpdatedAt"/>.</summary>
public sealed record MediaState(
    string Title,
    string Artist,
    string AppId,
    MediaKind Kind,
    bool IsPlaying,
    TimeSpan Position,
    TimeSpan Duration,
    DateTimeOffset PositionUpdatedAt,
    bool CanSeek,
    bool CanSkip,
    byte[]? Cover)
{
    public bool HasTimeline => Duration > TimeSpan.Zero;

    public TimeSpan PositionAt(DateTimeOffset now)
    {
        if (!IsPlaying) return Position;
        var position = Position + (now - PositionUpdatedAt);
        return HasTimeline && position > Duration ? Duration : position;
    }
}
