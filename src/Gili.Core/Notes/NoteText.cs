using System.Globalization;
using Gili.Core.Formatting;

namespace Gili.Core.Notes;

/// <summary>The texts on the Notes page: card title, search, the time on a card and the editor's status (mockup renderNotes).</summary>
public static class NoteText
{
    public const string Empty = "Empty note";
    public const string NoMatch = "No notes match.";
    public const string NoNotes = "No notes yet.";

    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    public static string Title(string body)
    {
        var end = body.IndexOf('\n');
        return (end < 0 ? body : body[..end]).Trim();
    }

    public static string Rest(string body)
    {
        var end = body.IndexOf('\n');
        return end < 0 ? "" : body[(end + 1)..].Trim();
    }

    /// <summary>The body as it is kept: every line break a single \n (the editor's TextBox types \r\n).</summary>
    public static string Stored(string body) => body.ReplaceLineEndings("\n");

    public static bool IsBlank(string body) => string.IsNullOrWhiteSpace(body);

    /// <summary>The card's accessible name: its title, or "Untitled note".</summary>
    public static string Label(string body) => Title(body) is { Length: > 0 } title ? title : "Untitled note";

    public static bool Matches(string body, string query)
    {
        var needle = query.Trim();
        return needle.Length == 0 || body.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The time on a card: the clock today, "Yesterday", a short weekday up to 6 days back, then "12 Oct" and "12 Oct 2025".</summary>
    public static string Edited(DateTimeOffset at, DateTimeOffset now, TimeZoneInfo zone, bool use24)
    {
        var local = TimeZoneInfo.ConvertTime(at, zone).DateTime;
        var today = TimeZoneInfo.ConvertTime(now, zone).DateTime;
        var days = (today.Date - local.Date).Days;
        if (days <= 0) return ClockText.Time(local, use24);
        if (days == 1) return "Yesterday";
        if (days < 7) return local.ToString("ddd", English);
        return local.ToString(local.Year == today.Year ? "d MMM" : "d MMM yyyy", English);
    }

    /// <summary>The editor's status once saved: "Saved · 19:20".</summary>
    public static string Saved(DateTimeOffset at, TimeZoneInfo zone, bool use24) =>
        $"Saved · {ClockText.Time(TimeZoneInfo.ConvertTime(at, zone).DateTime, use24)}";
}
