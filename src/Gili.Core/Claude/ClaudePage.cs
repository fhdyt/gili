using System.Globalization;

namespace Gili.Core.Claude;

/// <summary>How a row on the Claude page looks: yellow waiting, orange working, green done, dimmed idle.</summary>
public enum ClaudeRowKind { Ask, Work, Done, Idle }

/// <summary>One session on the Claude page (mockup renderClPage .crow): name, second line, and the status on the right.</summary>
public sealed record ClaudeRow(string Id, ClaudeRowKind Kind, string Name, string Sub, string Right, string Tooltip);

/// <summary>The Claude page's list (PRD 6.3 page 9, F-CLD-05): waiting, working, done (unseen), then idle sessions.</summary>
public static class ClaudePage
{
    public static IReadOnlyList<ClaudeRow> Rows(IReadOnlyList<ClaudeSession> sessions, DateTimeOffset now, bool use24Hour) =>
        [.. sessions.Select(s => Row(s, now, use24Hour)).OrderBy(r => r.Kind)];

    private static ClaudeRow Row(ClaudeSession s, DateTimeOffset now, bool use24Hour)
    {
        var kind = s.Status switch
        {
            ClaudeStatus.NeedsInput => ClaudeRowKind.Ask,
            ClaudeStatus.Working => ClaudeRowKind.Work,
            ClaudeStatus.Done => ClaudeRowKind.Done,
            _ => ClaudeRowKind.Idle,
        };
        var place = $"{s.Folder ?? s.Cwd ?? ""} · {ClaudeSlot.AppName(s)}";
        var sub = kind != ClaudeRowKind.Ask ? place
            : s.Need == ClaudeNeed.Question && !string.IsNullOrWhiteSpace(s.Snippet) ? $"\"{s.Snippet.Trim()}\""
            : !string.IsNullOrWhiteSpace(s.Snippet) ? s.Snippet.Trim()
            : s.Need switch
            {
                ClaudeNeed.Permission => "Claude needs permission",
                ClaudeNeed.Plan => "Plan ready for review",
                _ => "Claude needs your input",
            };
        var right = kind switch
        {
            ClaudeRowKind.Ask => "Needs input",
            ClaudeRowKind.Work => "Working · " + ClaudeSlot.Running(now - (s.PromptAt ?? now)),
            ClaudeRowKind.Done => "Done · " + ClaudeSlot.Took(s.DoneAfter ?? TimeSpan.Zero),
            _ => "Idle since " + Clock(s.IdleSince ?? now, use24Hour),
        };
        return new(s.Id, kind, s.Name, sub, right, $"{s.Name}\n{s.Cwd} · {ClaudeSlot.AppName(s)}");
    }

    /// <summary>14:05, or 2:05 PM with the 12-hour clock.</summary>
    private static string Clock(DateTimeOffset time, bool use24Hour) =>
        time.ToString(use24Hour ? "HH:mm" : "h:mm tt", CultureInfo.InvariantCulture);
}
