using TopBar.Core.Media;

namespace TopBar.Core.Tests.Media;

public class MediaRulesTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    private static MediaState Song(string title = "Blinding Lights", string artist = "The Weeknd", bool playing = true) =>
        new(title, artist, "Spotify.exe", MediaKind.Music, playing, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(200), T0,
            CanSeek: true, CanSkip: true, Cover: null);

    [Theory]
    [InlineData(PlaybackType.Video, "Chrome", MediaKind.Video)]
    [InlineData(PlaybackType.Music, "vlc.exe", MediaKind.Music)]
    [InlineData(PlaybackType.Unknown, "vlc.exe", MediaKind.Video)]
    [InlineData(PlaybackType.Unknown, "Microsoft.ZuneVideo_8wekyb3d8bbwe!Microsoft.ZuneVideo", MediaKind.Video)]
    [InlineData(PlaybackType.Unknown, "Spotify.exe", MediaKind.Music)]
    public void Classify_trusts_the_playback_type_then_known_video_apps(PlaybackType type, string appId, MediaKind expected) =>
        Assert.Equal(expected, MediaRules.Classify(type, appId));

    [Theory]
    [InlineData("Spotify.exe", "Spotify")]
    [InlineData("Chrome", "Google Chrome")]
    [InlineData("MSEdge", "Microsoft Edge")]
    [InlineData("Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic", "Media Player")]
    [InlineData("Contoso.Player_abc123!App", "App")]
    [InlineData("foobar2000.exe", "Foobar2000")]
    public void AppName_maps_known_apps_and_falls_back_to_the_id(string appId, string expected) =>
        Assert.Equal(expected, MediaRules.AppName(appId));

    [Theory]
    [InlineData("Spotify.exe", "#1DB954")]
    [InlineData("Chrome", "#30D158")]
    public void LevelColor_uses_the_brand_for_spotify_and_the_accent_otherwise(string appId, string expected) =>
        Assert.Equal(expected, MediaRules.LevelColor(appId));

    [Theory]
    [InlineData("lecture-03.mkv - VLC media player", "lecture-03.mkv")]
    [InlineData("VLC media player", null)]
    public void VlcTitle_strips_the_player_name(string windowTitle, string? expected) =>
        Assert.Equal(expected, MediaRules.VlcTitle(windowTitle));

    [Fact]
    public void Music_slot_shows_title_and_artist() =>
        Assert.Equal("Blinding Lights – The Weeknd", MediaRules.SlotTitle(Song()));

    [Fact]
    public void Music_slot_without_artist_shows_the_title() =>
        Assert.Equal("Blinding Lights", MediaRules.SlotTitle(Song(artist: "")));

    [Fact]
    public void Music_slot_is_cut_at_36_characters()
    {
        var text = MediaRules.SlotTitle(Song(title: "A Very Long Song Title That Keeps Going", artist: "Someone"));
        Assert.Equal(36, text.Length);
        Assert.EndsWith("…", text);
    }

    [Fact]
    public void Video_slot_is_cut_at_32_characters()
    {
        var video = Song(title: "Lecture 3: Linear Algebra and Its Applications") with { Kind = MediaKind.Video };
        Assert.Equal("Lecture 3: Linear Algebra and I…", MediaRules.SlotTitle(video));
    }

    [Fact]
    public void Truncate_drops_trailing_spaces_before_the_ellipsis() =>
        Assert.Equal("Hello…", MediaRules.Truncate("Hello  world", 8));

    [Theory]
    [InlineData(7, "0:07")]
    [InlineData(187, "3:07")]
    [InlineData(3723, "1:02:03")]
    [InlineData(-5, "0:00")]
    public void Time_is_minutes_and_seconds_with_hours_when_needed(int seconds, string expected) =>
        Assert.Equal(expected, MediaRules.Time(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void Remaining_counts_down_from_the_end() =>
        Assert.Equal("-2:10", MediaRules.Remaining(TimeSpan.FromSeconds(50), TimeSpan.FromSeconds(180)));

    [Theory]
    [InlineData(30, 120, 0.25)]
    [InlineData(30, 0, 0)]
    [InlineData(200, 120, 1)]
    public void Progress_is_a_fraction_of_the_duration(int position, int duration, double expected) =>
        Assert.Equal(expected, MediaRules.Progress(TimeSpan.FromSeconds(position), TimeSpan.FromSeconds(duration)));

    [Fact]
    public void A_playing_position_moves_on_with_time() =>
        Assert.Equal(TimeSpan.FromSeconds(40), Song().PositionAt(T0.AddSeconds(10)));

    [Fact]
    public void A_playing_position_stops_at_the_end() =>
        Assert.Equal(TimeSpan.FromSeconds(200), Song().PositionAt(T0.AddMinutes(10)));

    [Fact]
    public void A_paused_position_stays_put() =>
        Assert.Equal(TimeSpan.FromSeconds(30), Song(playing: false).PositionAt(T0.AddSeconds(10)));
}
