using System.Globalization;
using Gili.Core.Sound;
using DayFlags = Gili.Core.Time.Days;

namespace Gili.Core.Alarms;

/// <summary>The alarm texts on the Alarms page, in Tools and on the card (mockup Alarms page, CARDS.alarm).</summary>
public static class AlarmText
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
    private static readonly (DayFlags Day, string Name)[] Week =
    [
        (DayFlags.Monday, "Mon"), (DayFlags.Tuesday, "Tue"), (DayFlags.Wednesday, "Wed"), (DayFlags.Thursday, "Thu"),
        (DayFlags.Friday, "Fri"), (DayFlags.Saturday, "Sat"), (DayFlags.Sunday, "Sun"),
    ];

    public static string Time(TimeOnly time, bool use24Hour) =>
        time.ToString(use24Hour ? "HH:mm" : "h:mm tt", English);

    public static string Days(DayFlags repeat) => repeat switch
    {
        DayFlags.None => "Once",
        DayFlags.Every => "Every day",
        DayFlags.Weekdays => "Mon–Fri",
        _ => string.Join(", ", Week.Where(d => repeat.HasFlag(d.Day)).Select(d => d.Name)),
    };

    public static string Label(Alarm alarm) => string.IsNullOrWhiteSpace(alarm.Label) ? "Alarm" : alarm.Label.Trim();

    public static string RowDetail(Alarm alarm) => $"{Days(alarm.Repeat)} · {Voices.Find(alarm.Sound).Name}";

    /// <summary>The page's big time and the line under it: Today, Tomorrow, or the weekday, then the label.</summary>
    public static (string Big, string Sub) NextHeading(Alarm? next, DateTimeOffset now, TimeZoneInfo zone, bool use24Hour)
    {
        if (next?.NextFireAt is not { } at) return ("--:--", "No alarms on");
        var local = TimeZoneInfo.ConvertTime(at, zone);
        var days = local.Date.Subtract(TimeZoneInfo.ConvertTime(now, zone).Date).Days;
        var day = days switch
        {
            <= 0 => "Today",
            1 => "Tomorrow",
            _ => local.ToString("ddd", English),
        };
        return (Time(TimeOnly.FromDateTime(local.DateTime), use24Hour), $"{day} · {Label(next)}");
    }

    public static string CardTitle(Alarm alarm, bool missed, bool use24Hour) =>
        $"{(missed ? "Missed alarm" : "Alarm")} {Time(alarm.Time, use24Hour)}";

    /// <summary>What the editor accepts: H:mm, HH:mm, HHmm or HH.mm; minutes always two digits.</summary>
    public static TimeOnly? ParseTime(string text)
    {
        var t = text.Trim().Replace('.', ':');
        if (t.Length == 4 && !t.Contains(':')) t = t[..2] + ":" + t[2..];
        var parts = t.Split(':');
        if (parts.Length != 2 || parts[1].Length != 2 || parts[0].Length is < 1 or > 2) return null;
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hour) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minute)) return null;
        return hour is >= 0 and < 24 && minute is >= 0 and < 60 ? new TimeOnly(hour, minute) : null;
    }
}
