using System.Globalization;
using System.Net;
using System.Net.Http;
using Gili.Core.About;
using Gili.Core.Lyrics;

namespace Gili.Platform.Media;

/// <summary>
/// Synced lyrics from LRCLIB (F-ISL-26, PRD 6.3 "Lirik lagu"): <c>/api/get</c> by title, artist, album and length, then
/// <c>/api/search</c> by title and artist. Only those four fields leave the computer, with the User-Agent LRCLIB asks for.
/// </summary>
public static class LrclibClient
{
    private static readonly HttpClient Client = Create();

    /// <summary>The lyrics' status and lines; Offline when LRCLIB can't be reached.</summary>
    public static async Task<(LyricsStatus Status, IReadOnlyList<LyricLine>? Lines)> FindAsync(
        string title, string artist, string? album, TimeSpan duration, CancellationToken cancel)
    {
        var track = LyricsSearch.CleanTitle(title);
        var who = LyricsSearch.CleanArtist(artist);
        try
        {
            var query = $"track_name={Uri.EscapeDataString(track)}&artist_name={Uri.EscapeDataString(who)}";
            if (!string.IsNullOrWhiteSpace(album)) query += "&album_name=" + Uri.EscapeDataString(album);
            if (duration > TimeSpan.Zero) query += "&duration=" + Math.Round(duration.TotalSeconds).ToString(CultureInfo.InvariantCulture);
            using (var exact = await Client.GetAsync("api/get?" + query, cancel))
            {
                if (exact.IsSuccessStatusCode)
                {
                    var found = LyricsSearch.Read(LrclibRecord.ParseOne(await exact.Content.ReadAsStringAsync(cancel)));
                    if (found.Status != LyricsStatus.NotFound) return found;
                }
                else if (exact.StatusCode != HttpStatusCode.NotFound) return (LyricsStatus.Offline, null);
            }
            var search = $"track_name={Uri.EscapeDataString(track)}&artist_name={Uri.EscapeDataString(who)}";
            using var many = await Client.GetAsync("api/search?" + search, cancel);
            if (!many.IsSuccessStatusCode) return (LyricsStatus.Offline, null);
            var results = LrclibRecord.ParseMany(await many.Content.ReadAsStringAsync(cancel));
            return LyricsSearch.Read(LyricsSearch.Pick(results, duration));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancel.IsCancellationRequested)
        {
            return (LyricsStatus.Offline, null);
        }
    }

    private static HttpClient Create()
    {
        var client = new HttpClient { BaseAddress = new Uri("https://lrclib.net/"), Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Gili/" + AboutInfo.Format(typeof(LrclibClient).Assembly.GetName().Version!));
        return client;
    }
}
