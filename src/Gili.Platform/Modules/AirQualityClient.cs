using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace Gili.Platform.Modules;

/// <summary>The current US AQI and PM2.5 (µg/m³); PM2.5 is null when the model has none for the place.</summary>
public sealed record AirReading(int UsAqi, double? Pm25);

/// <summary>
/// The air now from Open-Meteo's air quality API (F-RIGHT-13): no API key, and only the Weather city's coordinates leave
/// the computer (PRD 8.2). Its US AQI is the index IQAir shows.
/// </summary>
public static class AirQualityClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static async Task<AirReading> GetAsync(double latitude, double longitude, CancellationToken cancel)
    {
        var url = string.Create(CultureInfo.InvariantCulture,
            $"https://air-quality-api.open-meteo.com/v1/air-quality?latitude={latitude}&longitude={longitude}&current=us_aqi,pm2_5");
        await using var stream = await Http.GetStreamAsync(url, cancel).ConfigureAwait(false);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancel).ConfigureAwait(false);
        var current = json.RootElement.GetProperty("current");
        // A null US AQI throws InvalidOperationException, which the caller treats as a failed read.
        var aqi = (int)Math.Round(current.GetProperty("us_aqi").GetDouble());
        double? pm25 = current.TryGetProperty("pm2_5", out var pm) && pm.ValueKind == JsonValueKind.Number ? pm.GetDouble() : null;
        return new AirReading(aqi, pm25);
    }
}
