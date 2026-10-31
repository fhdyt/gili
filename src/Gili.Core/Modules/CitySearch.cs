using System.Text.Json;

namespace Gili.Core.Modules;

/// <summary>A place Open-Meteo's geocoding found: its name, region and country, and where it is.</summary>
public sealed record CityResult(string Name, string? Region, string? Country, double Latitude, double Longitude)
{
    /// <summary>"Jakarta, Indonesia", or "Springfield, Illinois, United States" when the region tells two apart.</summary>
    public string Display => string.Join(", ", new[] { Name, Region == Name ? null : Region, Country }.Where(p => !string.IsNullOrWhiteSpace(p)));
}

/// <summary>
/// The Weather city row's search (F-RIGHT-05, design 4c): Open-Meteo's geocoding answer read into results. Only the typed
/// name is sent, and only while the row searches (PRD 8.2).
/// </summary>
public static class CitySearch
{
    public const int Count = 5;

    /// <summary>A name worth asking about: two letters or more.</summary>
    public static bool IsQuery(string? text) => text is not null && text.Trim().Length >= 2;

    public static string Url(string name) =>
        $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(name.Trim())}&count={Count}&language=en&format=json";

    /// <summary>The results in the API's order (most relevant first); none for an empty or odd answer.</summary>
    public static IReadOnlyList<CityResult> Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array) return [];
            var list = new List<CityResult>();
            foreach (var r in results.EnumerateArray())
            {
                if (Text(r, "name") is not { } name || !r.TryGetProperty("latitude", out var lat) || !r.TryGetProperty("longitude", out var lon)) continue;
                list.Add(new CityResult(name, Text(r, "admin1"), Text(r, "country"), lat.GetDouble(), lon.GetDouble()));
            }
            return list;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
