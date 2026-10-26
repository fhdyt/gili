using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace Gili.Platform.Modules;

public sealed record WeatherReading(double Temperature, int Code, bool Day, double Max, double Min);

/// <summary>
/// Today's weather from Open-Meteo (PRD 9.2): no API key, and only the city's coordinates leave the computer (PRD 8.2).
/// </summary>
public static class WeatherClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static async Task<WeatherReading> GetAsync(double latitude, double longitude, CancellationToken cancel)
    {
        var url = string.Create(CultureInfo.InvariantCulture,
            $"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&current=temperature_2m,weather_code,is_day&daily=temperature_2m_max,temperature_2m_min&timezone=auto&forecast_days=1");
        await using var stream = await Http.GetStreamAsync(url, cancel).ConfigureAwait(false);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancel).ConfigureAwait(false);
        var current = json.RootElement.GetProperty("current");
        var daily = json.RootElement.GetProperty("daily");
        return new WeatherReading(
            current.GetProperty("temperature_2m").GetDouble(),
            current.GetProperty("weather_code").GetInt32(),
            current.GetProperty("is_day").GetInt32() == 1,
            daily.GetProperty("temperature_2m_max")[0].GetDouble(),
            daily.GetProperty("temperature_2m_min")[0].GetDouble());
    }
}
