using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Gili.Core.Lyrics;

/// <summary>One timed line of a song; an empty <paramref name="Text"/> is an instrumental break (PRD 6.3 "Lirik lagu").</summary>
public sealed record LyricLine(TimeSpan At, string Text);

/// <summary>Where the lyrics of the playing song stand (F-ISL-26).</summary>
public enum LyricsStatus { Off, Searching, Synced, NotFound, Unsynced, Offline }

/// <summary>One LRCLIB record: <c>/api/get</c> answers one, <c>/api/search</c> a list of them.</summary>
public sealed record LrclibRecord(string Track, string Artist, string? Album, double Duration, bool Instrumental, string? Plain, string? Synced)
{
    public static LrclibRecord? ParseOne(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return From(doc.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static IReadOnlyList<LrclibRecord> ParseMany(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];
            return [.. doc.RootElement.EnumerateArray().Select(From).OfType<LrclibRecord>()];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static LrclibRecord? From(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object) return null;
        string? Text(string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        var duration = e.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetDouble() : 0;
        var instrumental = e.TryGetProperty("instrumental", out var i) && i.ValueKind == JsonValueKind.True;
        return new LrclibRecord(Text("trackName") ?? "", Text("artistName") ?? "", Text("albumName"), duration, instrumental, Text("plainLyrics"), Text("syncedLyrics"));
    }
}

/// <summary>Reading LRC text: <c>[mm:ss.xx] words</c>, several stamps on one line, an <c>[offset:+/-ms]</c> tag, other tags skipped.</summary>
public static partial class Lrc
{
    /// <summary>The timed lines in order; null when the text has no timed line at all (lyrics that aren't synced).</summary>
    public static IReadOnlyList<LyricLine>? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var offset = TimeSpan.Zero;
        var lines = new List<LyricLine>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var tag = OffsetTag().Match(line);
            if (tag.Success)
            {
                offset = TimeSpan.FromMilliseconds(int.Parse(tag.Groups[1].Value, CultureInfo.InvariantCulture));
                continue;
            }
            var stamps = new List<TimeSpan>();
            var rest = line;
            Match m;
            while ((m = Stamp().Match(rest)).Success)
            {
                var minutes = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                var seconds = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                stamps.Add(TimeSpan.FromSeconds(minutes * 60 + seconds));
                rest = rest[m.Length..];
            }
            var words = rest.Trim();
            foreach (var at in stamps) lines.Add(new LyricLine(at, words));
        }
        if (lines.Count == 0) return null;
        // A positive offset shows the lyrics sooner (the LRC convention).
        return [.. lines.Select(l => l with { At = l.At - offset < TimeSpan.Zero ? TimeSpan.Zero : l.At - offset }).OrderBy(l => l.At)];
    }

    [GeneratedRegex(@"^\[(\d{1,3}):(\d{1,2}(?:[.:]\d{1,3})?)\]")]
    private static partial Regex Stamp();

    [GeneratedRegex(@"^\[offset:\s*([+-]?\d+)\s*\]", RegexOptions.IgnoreCase)]
    private static partial Regex OffsetTag();
}

/// <summary>How a song is looked up on LRCLIB (PRD 6.3): its title cleaned of extras, and the result whose length fits.</summary>
public static partial class LyricsSearch
{
    /// <summary>A search result's length may differ from the song's by this much.</summary>
    public static readonly TimeSpan Tolerance = TimeSpan.FromSeconds(2);

    /// <summary>"Song (Remastered 2011)", "Song - Live", "Song (feat. X)", "Song [Official Video]" are all "Song".</summary>
    public static string CleanTitle(string title)
    {
        var t = Brackets().Replace(title, "");
        t = DashSuffix().Replace(t, "");
        t = Feat().Replace(t, "");
        return Spaces().Replace(t, " ").Trim(' ', '-', '–');
    }

    /// <summary>YouTube Music's "Artist - Topic" channels, and "A, B" or "A & B" credits, become the first artist.</summary>
    public static string CleanArtist(string artist)
    {
        var a = TopicSuffix().Replace(artist, "");
        var cut = a.IndexOfAny([',', '&']);
        if (cut > 0) a = a[..cut];
        a = Feat().Replace(a, "");
        return a.Trim();
    }

    /// <summary>The synced result closest in length within the tolerance; else an unsynced one, so the page can say so; else null.</summary>
    public static LrclibRecord? Pick(IEnumerable<LrclibRecord> results, TimeSpan duration)
    {
        var near = results.Where(r => duration <= TimeSpan.Zero || Math.Abs(r.Duration - duration.TotalSeconds) <= Tolerance.TotalSeconds)
            .OrderBy(r => Math.Abs(r.Duration - duration.TotalSeconds)).ToList();
        return near.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.Synced)) ?? near.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.Plain));
    }

    /// <summary>What a record means for the page: synced lines, or why there are none.</summary>
    public static (LyricsStatus Status, IReadOnlyList<LyricLine>? Lines) Read(LrclibRecord? record)
    {
        if (record is null || record.Instrumental) return (LyricsStatus.NotFound, null);
        if (Lrc.Parse(record.Synced) is { } lines) return (LyricsStatus.Synced, lines);
        return string.IsNullOrWhiteSpace(record.Plain) ? (LyricsStatus.NotFound, null) : (LyricsStatus.Unsynced, null);
    }

    [GeneratedRegex(@"\s*[\(\[][^\)\]]*(remaster|live|feat\.?|ft\.|with |version|edit|mono|stereo|official|video|audio|lyric|visuali[sz]er|mv|hd|4k)[^\)\]]*[\)\]]", RegexOptions.IgnoreCase)]
    private static partial Regex Brackets();

    [GeneratedRegex(@"\s+[-–]\s+(\d{4}\s+)?(remaster(ed)?|live|radio edit|single version|mono|stereo)\b.*$", RegexOptions.IgnoreCase)]
    private static partial Regex DashSuffix();

    [GeneratedRegex(@"\s+(feat\.?|ft\.|featuring)\s+.*$", RegexOptions.IgnoreCase)]
    private static partial Regex Feat();

    [GeneratedRegex(@"\s+-\s+Topic$", RegexOptions.IgnoreCase)]
    private static partial Regex TopicSuffix();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex Spaces();
}

/// <summary>Which line is playing, and the three lines the Media page shows around it (PRD 6.3).</summary>
public static class LyricsView
{
    public const int SlotLength = 44;
    public const string Break = "♪";

    /// <summary>The last line that has started by <paramref name="position"/>; −1 before the first.</summary>
    public static int Index(IReadOnlyList<LyricLine> lines, TimeSpan position)
    {
        var index = -1;
        for (var i = 0; i < lines.Count && lines[i].At <= position; i++) index = i;
        return index;
    }

    /// <summary>The previous, current and next line; a break shows as ♪, nothing before the first line or after the last.</summary>
    public static (string Previous, string Current, string Next) Window(IReadOnlyList<LyricLine> lines, int index)
    {
        string At(int i) => i < 0 || i >= lines.Count ? "" : lines[i].Text.Length == 0 ? Break : lines[i].Text;
        return (At(index - 1), index < 0 ? Break : At(index), At(index + 1));
    }

    /// <summary>The slot's line: the current one cut to 44 characters, or null before the first line and during a break.</summary>
    public static string? Slot(IReadOnlyList<LyricLine> lines, int index) =>
        index < 0 || index >= lines.Count || lines[index].Text.Length == 0 ? null : Media.MediaRules.Truncate(lines[index].Text, SlotLength);

    /// <summary>The middle line's text while there are no lines to show.</summary>
    public static string Message(LyricsStatus status) => status switch
    {
        LyricsStatus.Searching => "Searching for lyrics…",
        LyricsStatus.NotFound => "No lyrics for this song",
        LyricsStatus.Unsynced => "Lyrics aren't synced for this song",
        LyricsStatus.Offline => "Lyrics unavailable offline",
        _ => "",
    };
}

/// <summary>The last 20 songs' lyrics, in memory only (PRD 6.3: never on disk or in the log).</summary>
public sealed class LyricsCache
{
    public const int Size = 20;
    private readonly LinkedList<(string Key, LyricsStatus Status, IReadOnlyList<LyricLine>? Lines)> _items = new();

    public bool TryGet(string key, out LyricsStatus status, out IReadOnlyList<LyricLine>? lines)
    {
        for (var node = _items.First; node is not null; node = node.Next)
        {
            if (node.Value.Key != key) continue;
            _items.Remove(node);
            _items.AddFirst(node);
            (status, lines) = (node.Value.Status, node.Value.Lines);
            return true;
        }
        (status, lines) = (LyricsStatus.Off, null);
        return false;
    }

    /// <summary>Offline answers are not kept, so the song is asked again once the connection is back.</summary>
    public void Add(string key, LyricsStatus status, IReadOnlyList<LyricLine>? lines)
    {
        if (status is LyricsStatus.Offline or LyricsStatus.Searching or LyricsStatus.Off) return;
        TryGet(key, out _, out _);
        if (_items.First?.Value.Key == key) _items.RemoveFirst();
        _items.AddFirst((key, status, lines));
        while (_items.Count > Size) _items.RemoveLast();
    }

    public int Count => _items.Count;
}
