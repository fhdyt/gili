using System.Globalization;

namespace Gili.Core.Modules;

public enum WeatherIcon { Sun, Moon, CloudSun, Cloud, Fog, Drizzle, Rain, Snow, Storm }

/// <summary>Weather as the bar shows it (PRD 6.4): a short label over the temperature, and a tooltip.</summary>
public static class WeatherText
{
    /// <summary>The bar label, the longer condition for the tooltip, and the icon, from an Open-Meteo WMO weather code.</summary>
    public static (string Label, string Long, WeatherIcon Icon) For(int code, bool day) => code switch
    {
        0 => ("Clear", "Clear sky", day ? WeatherIcon.Sun : WeatherIcon.Moon),
        1 => ("Clear", "Mainly clear", day ? WeatherIcon.Sun : WeatherIcon.Moon),
        2 => ("Cloudy", "Partly cloudy", WeatherIcon.CloudSun),
        3 => ("Overcast", "Overcast", WeatherIcon.Cloud),
        45 or 48 => ("Fog", "Fog", WeatherIcon.Fog),
        >= 51 and <= 57 => ("Drizzle", "Drizzle", WeatherIcon.Drizzle),
        >= 61 and <= 67 => ("Rain", "Rain", WeatherIcon.Rain),
        (>= 71 and <= 77) or 85 or 86 => ("Snow", "Snow", WeatherIcon.Snow),
        >= 80 and <= 82 => ("Showers", "Rain showers", WeatherIcon.Rain),
        >= 95 and <= 99 => ("Storm", "Thunderstorm", WeatherIcon.Storm),
        _ => ("Weather", "Weather", WeatherIcon.Cloud),
    };

    public static string Temperature(double celsius) => Math.Round(celsius).ToString(CultureInfo.InvariantCulture) + "°";

    /// <summary>"Partly cloudy · 31° / 24° · Jakarta".</summary>
    public static string Tooltip(string condition, double max, double min, string city) =>
        $"{condition} · {Temperature(max)} / {Temperature(min)} · {city}";
}

public static class WifiText
{
    private const int MaxName = 16;

    public static string Strength(int quality) => quality switch
    {
        >= 75 => "Strong",
        >= 50 => "Good",
        >= 25 => "Fair",
        _ => "Weak",
    };

    public static string Name(string ssid) => ssid.Length <= MaxName ? ssid : ssid[..(MaxName - 1)] + "…";
}

public enum VolumeLevel { Mute, Low, Mid, High }

/// <summary>The speaker on the bar follows the level: muted, quiet (under 34%), medium, loud (67% and up).</summary>
public static class VolumeText
{
    public static VolumeLevel Level(int percent, bool muted) =>
        muted || percent <= 0 ? VolumeLevel.Mute : percent < 34 ? VolumeLevel.Low : percent < 67 ? VolumeLevel.Mid : VolumeLevel.High;

    public static string Tooltip(int percent, bool muted) =>
        muted ? "Muted" : "Volume " + percent.ToString(CultureInfo.InvariantCulture) + "%";
}

/// <summary>The battery line in the Power island: "64% · 2 h 10 min", "Charging · 64%", or nothing without a battery.</summary>
public static class BatteryText
{
    public static string Line(int? percent, bool charging, TimeSpan? remaining)
    {
        if (percent is not { } p) return "";
        var value = p.ToString(CultureInfo.InvariantCulture) + "%";
        if (charging) return "Charging · " + value;
        if (remaining is not { } left || left <= TimeSpan.Zero) return value;
        var hours = (int)left.TotalHours;
        var time = hours > 0 ? $"{hours} h {left.Minutes} min" : $"{left.Minutes} min";
        return value + " · " + time;
    }
}

/// <summary>
/// Modules that don't fit move into "…" in this order (PRD 6.4); Wi‑Fi, Bluetooth, Volume, Brightness, Tray, Clock,
/// Settings and Power always stay.
/// </summary>
public static class RightOverflow
{
    private static readonly string[] Order = ["weather", "net", "disk", "gpu", "ram", "cpu"];
    private const double MoreWidth = 32;

    public static IReadOnlyList<string> Hide(IReadOnlyList<(string Id, double Width)> modules, double available)
    {
        var hidden = new List<string>();
        var width = modules.Sum(m => m.Width);
        foreach (var id in Order)
        {
            if (width + (hidden.Count > 0 ? MoreWidth : 0) <= available) break;
            var module = modules.FirstOrDefault(m => m.Id == id);
            if (module.Id is null) continue;
            hidden.Add(id);
            width -= module.Width;
        }
        return hidden;
    }
}

/// <summary>A failing provider is tried again after 5 s, 30 s, then every 2 minutes (PRD 11).</summary>
public static class ProviderRetry
{
    public static TimeSpan Delay(int failures) => failures switch
    {
        <= 1 => TimeSpan.FromSeconds(5),
        2 => TimeSpan.FromSeconds(30),
        _ => TimeSpan.FromMinutes(2),
    };
}
