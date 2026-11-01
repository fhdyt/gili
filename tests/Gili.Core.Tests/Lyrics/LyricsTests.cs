using Gili.Core.Lyrics;
using Gili.Core.Settings;

namespace Gili.Core.Tests.Lyrics;

public class LyricsTests
{
    private const string Song = "[ti:Evaluasi]\n[00:12.30] First line\n[00:15.80]\n[00:18.05][01:02.50] Chorus line\n[00:20.5] Short stamp";

    [Fact]
    public void Lrc_lines_come_in_time_order_with_breaks_and_repeats()
    {
        var lines = Lrc.Parse(Song)!;
        Assert.Equal(5, lines.Count);
        Assert.Equal(new LyricLine(TimeSpan.FromSeconds(12.3), "First line"), lines[0]);
        Assert.Equal("", lines[1].Text);
        Assert.Equal(TimeSpan.FromSeconds(20.5), lines[3].At);
        Assert.Equal(new LyricLine(TimeSpan.FromSeconds(62.5), "Chorus line"), lines[4]);
    }

    [Fact]
    public void An_offset_tag_moves_every_line()
    {
        var lines = Lrc.Parse("[offset:+500]\n[00:10.00] a")!;
        Assert.Equal(TimeSpan.FromSeconds(9.5), lines[0].At);
    }

    [Fact]
    public void A_colon_before_the_hundredths_and_a_huge_offset_never_throw()
    {
        Assert.Equal(TimeSpan.FromSeconds(12.34), Lrc.Parse("[00:12:34] colon form")![0].At);
        Assert.Equal(TimeSpan.FromSeconds(1), Lrc.Parse("[offset:99999999999999]" + (char)10 + "[00:01.00] a")![0].At);
    }

    [Fact]
    public void Text_without_stamps_is_not_synced()
    {
        Assert.Null(Lrc.Parse("Just words\nMore words"));
        Assert.Null(Lrc.Parse(null));
        Assert.Null(Lrc.Parse("  "));
    }

    [Theory]
    [InlineData("Here Comes the Sun (Remastered 2009)", "Here Comes the Sun")]
    [InlineData("Evaluasi - Live", "Evaluasi")]
    [InlineData("Bohemian Rhapsody - 2011 Remaster", "Bohemian Rhapsody")]
    [InlineData("Stay (feat. Justin Bieber)", "Stay")]
    [InlineData("Peaches ft. Daniel Caesar", "Peaches")]
    [InlineData("Hindia - Evaluasi [Official Music Video]", "Hindia - Evaluasi")]
    [InlineData("Rumah ke Rumah", "Rumah ke Rumah")]
    public void Titles_lose_their_extras(string title, string clean) => Assert.Equal(clean, LyricsSearch.CleanTitle(title));

    [Theory]
    [InlineData("Hindia - Topic", "Hindia")]
    [InlineData("Daft Punk, Pharrell Williams", "Daft Punk")]
    [InlineData("Simon & Garfunkel", "Simon")]
    [InlineData("M83", "M83")]
    public void Artists_become_the_first_one(string artist, string clean) => Assert.Equal(clean, LyricsSearch.CleanArtist(artist));

    [Fact]
    public void The_synced_result_closest_in_length_wins()
    {
        var results = new[]
        {
            new LrclibRecord("a", "b", null, 200, false, "plain", null),
            new LrclibRecord("a", "b", null, 239, false, null, "[00:01.00] near"),
            new LrclibRecord("a", "b", null, 300, false, null, "[00:01.00] far"),
        };
        Assert.Equal(239, LyricsSearch.Pick(results, TimeSpan.FromSeconds(238))!.Duration);
        Assert.Null(LyricsSearch.Pick(results, TimeSpan.FromSeconds(260)));
        Assert.Equal(200, LyricsSearch.Pick(results, TimeSpan.FromSeconds(201))!.Duration);
    }

    [Fact]
    public void Records_say_whether_they_are_synced()
    {
        Assert.Equal(LyricsStatus.Synced, LyricsSearch.Read(new LrclibRecord("a", "b", null, 1, false, "x", "[00:01.00] x")).Status);
        Assert.Equal(LyricsStatus.Unsynced, LyricsSearch.Read(new LrclibRecord("a", "b", null, 1, false, "x", null)).Status);
        Assert.Equal(LyricsStatus.NotFound, LyricsSearch.Read(new LrclibRecord("a", "b", null, 1, true, null, null)).Status);
        Assert.Equal(LyricsStatus.NotFound, LyricsSearch.Read(null).Status);
    }

    [Fact]
    public void Lrclib_json_reads_one_or_many()
    {
        var one = LrclibRecord.ParseOne("""{"trackName":"Evaluasi","artistName":"Hindia","albumName":"Menari","duration":238.0,"instrumental":false,"plainLyrics":"x","syncedLyrics":"[00:01.00] x"}""")!;
        Assert.Equal(("Evaluasi", 238.0), (one.Track, one.Duration));
        Assert.Equal(2, LrclibRecord.ParseMany("""[{"trackName":"a","duration":1},{"trackName":"b","duration":2}]""").Count);
        Assert.Empty(LrclibRecord.ParseMany("{\"code\":404}"));
        Assert.Null(LrclibRecord.ParseOne("not json"));
    }

    [Fact]
    public void The_window_follows_the_position()
    {
        var lines = Lrc.Parse(Song)!;
        Assert.Equal(-1, LyricsView.Index(lines, TimeSpan.FromSeconds(5)));
        Assert.Equal(0, LyricsView.Index(lines, TimeSpan.FromSeconds(12.3)));
        Assert.Equal(1, LyricsView.Index(lines, TimeSpan.FromSeconds(16)));
        Assert.Equal(("First line", "♪", "Chorus line"), LyricsView.Window(lines, 1));
        Assert.Equal(("", "♪", "First line"), LyricsView.Window(lines, -1));
        Assert.Equal(("Short stamp", "Chorus line", ""), LyricsView.Window(lines, 4));
    }

    [Fact]
    public void The_slot_shows_a_line_only_while_one_is_sung()
    {
        var lines = Lrc.Parse(Song)!;
        Assert.Null(LyricsView.Slot(lines, -1));
        Assert.Equal("First line", LyricsView.Slot(lines, 0));
        Assert.Null(LyricsView.Slot(lines, 1));
        var longLine = new[] { new LyricLine(TimeSpan.Zero, new string('a', 60)) };
        Assert.Equal(44, LyricsView.Slot(longLine, 0)!.Length);
    }

    [Fact]
    public void The_cache_keeps_twenty_and_forgets_offline_answers()
    {
        var cache = new LyricsCache();
        for (var i = 0; i < 25; i++) cache.Add("song" + i, LyricsStatus.NotFound, null);
        Assert.Equal(LyricsCache.Size, cache.Count);
        Assert.False(cache.TryGet("song0", out _, out _));
        Assert.True(cache.TryGet("song24", out var status, out _));
        Assert.Equal(LyricsStatus.NotFound, status);
        cache.Add("offline", LyricsStatus.Offline, null);
        Assert.False(cache.TryGet("offline", out _, out _));
    }

    [Fact]
    public void Show_lyrics_is_off_until_turned_on()
    {
        Assert.False(new AppSettings().ShowLyrics);
        Assert.True(SettingsCatalog.Write(new AppSettings(), "lyrics", true).ShowLyrics);
        Assert.Contains(SettingsCatalog.Sections.SelectMany(s => s.Rows), r => r.Key == "lyrics" && r.Available);
    }
}
