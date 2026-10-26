using Gili.Core.Media;

namespace Gili.Core.Tests.Media;

public class MediaPresenceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
    private readonly MediaPresence _presence = new();

    private static MediaState Song(bool playing) =>
        new("Song", "Artist", "Spotify.exe", MediaKind.Music, playing, TimeSpan.Zero, TimeSpan.FromMinutes(3), T0, true, true, null);

    [Fact]
    public void Playing_media_holds_the_slot() => Assert.True(_presence.IsActive(Song(playing: true), T0));

    [Fact]
    public void Media_paused_after_playing_keeps_the_slot_for_two_minutes()
    {
        _presence.IsActive(Song(playing: true), T0);
        Assert.True(_presence.IsActive(Song(playing: false), T0.AddSeconds(1)));
        Assert.True(_presence.IsActive(Song(playing: false), T0.AddSeconds(121)));
        Assert.False(_presence.IsActive(Song(playing: false), T0.AddSeconds(122)));
    }

    [Fact]
    public void Media_found_paused_does_not_take_the_slot() =>
        Assert.False(_presence.IsActive(Song(playing: false), T0));

    [Fact]
    public void Playing_again_resets_the_grace_period()
    {
        _presence.IsActive(Song(playing: true), T0);
        _presence.IsActive(Song(playing: false), T0.AddSeconds(1));
        _presence.IsActive(Song(playing: true), T0.AddSeconds(100));
        Assert.True(_presence.IsActive(Song(playing: false), T0.AddSeconds(200)));
    }

    [Fact]
    public void Losing_the_session_forgets_that_it_played()
    {
        _presence.IsActive(Song(playing: true), T0);
        Assert.False(_presence.IsActive(null, T0.AddSeconds(1)));
        Assert.False(_presence.IsActive(Song(playing: false), T0.AddSeconds(2)));
    }
}
