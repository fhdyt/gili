using System.Globalization;

namespace Gili.Core.Formatting;

/// <summary>Clock and date: "19:38" / "7:38 PM" and "Mon 26 Oct", and the bar's two-line clock on the right (PRD 6.4).</summary>
public static class ClockText
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    public static string Time(DateTime local, bool use24Hour) =>
        local.ToString(use24Hour ? "HH:mm" : "h:mm tt", English);

    public static string Date(DateTime local) => local.ToString("ddd d MMM", English);

    /// <summary>Time left until the next minute starts, so the clock flips exactly on the minute (F-ISL-01).</summary>
    public static TimeSpan UntilNextMinute(DateTime now)
    {
        var minuteStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, now.Kind);
        return minuteStart.AddMinutes(1) - now;
    }

    /// <summary>The bar clock's value: "14:32:05" / "2:32:05 PM", or without seconds when Show seconds is off (PRD 6.4).</summary>
    public static string BarTime(DateTime local, bool use24Hour, bool seconds) =>
        local.ToString((use24Hour ? "HH:mm" : "h:mm") + (seconds ? ":ss" : "") + (use24Hour ? "" : " tt"), English);

    /// <summary>The bar clock's label: "SAT 25 OCT".</summary>
    public static string BarDate(DateTime local) => local.ToString("ddd d MMM", English).ToUpperInvariant();

    /// <summary>The clock's tooltip: "Saturday, 25 October 2026".</summary>
    public static string LongDate(DateTime local) => local.ToString("dddd, d MMMM yyyy", English);

    /// <summary>Time left until the next second starts, so the seconds flip with the system clock (F-RIGHT-11).</summary>
    public static TimeSpan UntilNextSecond(DateTime now) => TimeSpan.FromTicks(TimeSpan.TicksPerSecond - now.Ticks % TimeSpan.TicksPerSecond);
}
