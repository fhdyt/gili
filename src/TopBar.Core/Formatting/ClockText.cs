using System.Globalization;

namespace TopBar.Core.Formatting;

/// <summary>Clock and date as the compact island shows them: "19:38" / "7:38 PM" and "Mon 26 Oct" (PRD 6.3).</summary>
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
}
