namespace TopBar.Core.Media;

/// <summary>
/// Whether media holds the compact slot. Playing media always does. Media paused after playing keeps it for two
/// minutes so it can be resumed from the island, then the clock comes back. A session found paused never takes it.
/// </summary>
public sealed class MediaPresence
{
    public static readonly TimeSpan PausedGrace = TimeSpan.FromMinutes(2);

    private bool _played;
    private DateTimeOffset? _pausedAt;

    public bool IsActive(MediaState? media, DateTimeOffset now)
    {
        if (media is null)
        {
            _played = false;
            _pausedAt = null;
            return false;
        }
        if (media.IsPlaying)
        {
            _played = true;
            _pausedAt = null;
            return true;
        }
        if (!_played) return false;
        _pausedAt ??= now;
        return now - _pausedAt.Value <= PausedGrace;
    }
}
