using System.Net.Http;
using Gili.Core.Modules;

namespace Gili.Platform.Modules;

/// <summary>Open-Meteo's geocoding for the Weather city row (design 4c): no API key; only the typed name is sent.</summary>
public static class GeocodingClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public static async Task<IReadOnlyList<CityResult>> SearchAsync(string name, CancellationToken cancel)
    {
        var json = await Http.GetStringAsync(CitySearch.Url(name), cancel).ConfigureAwait(false);
        return CitySearch.Parse(json);
    }
}
