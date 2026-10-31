using System.Globalization;

namespace Gili.Core.Claude;

/// <summary>What the compact slot shows for Claude, best first: a session waiting, a Done, then work (PRD 6.3).</summary>
public enum ClaudeSlotState { None, NeedsInput, Done, Working }

/// <summary>
/// The slot's Claude activity. <paramref name="Name"/> is the one session's name (at most 28 characters), absent when
/// several are counted in <paramref name="Text"/>; <paramref name="Value"/> is the running time of one working session.
/// </summary>
public sealed record ClaudeSlotView(ClaudeSlotState State, string? Name, string Text, string? Value, string Tooltip)
{
    public static readonly ClaudeSlotView Empty = new(ClaudeSlotState.None, null, "", null, "");
}

/// <summary>The mockup's clSlotHTML, clTip, durTxt and fmtDur as plain text.</summary>
public static class ClaudeSlot
{
    private const int NameLength = 28;

    public static ClaudeSlotView For(IReadOnlyList<ClaudeSession> sessions, DateTimeOffset now)
    {
        var ask = sessions.Where(s => s.Status == ClaudeStatus.NeedsInput).ToList();
        var work = sessions.Where(s => s.Status == ClaudeStatus.Working).ToList();
        var done = sessions.Where(s => s.Status == ClaudeStatus.Done).MaxBy(s => s.DoneUntil);
        if (ask.Count > 0)
        {
            return ask.Count > 1
                ? new(ClaudeSlotState.NeedsInput, null, $"{ask.Count} sessions need input", null, Tooltip(ask[0], snippet: true))
                : new(ClaudeSlotState.NeedsInput, Trunc(ask[0].Name), "Needs input", null, Tooltip(ask[0], snippet: true));
        }
        if (done is not null)
            return new(ClaudeSlotState.Done, Trunc(done.Name), "Done · " + Took(done.DoneAfter ?? TimeSpan.Zero), null, Tooltip(done, snippet: false));
        if (work.Count > 1) return new(ClaudeSlotState.Working, null, $"{work.Count} sessions working", null, Tooltip(work[0], snippet: false));
        if (work.Count == 1)
        {
            var s = work[0];
            return new(ClaudeSlotState.Working, Trunc(s.Name), "", Running(now - (s.PromptAt ?? now)), Tooltip(s, snippet: false));
        }
        return ClaudeSlotView.Empty;
    }

    /// <summary>The mockup's durTxt: 38s, 4m 12s, 1h 5m.</summary>
    public static string Took(TimeSpan time)
    {
        var seconds = Math.Max(1, (long)Math.Round(time.TotalSeconds));
        var minutes = seconds / 60;
        return minutes >= 60 ? $"{minutes / 60}h {minutes % 60}m" : minutes > 0 ? $"{minutes}m {seconds % 60}s" : $"{seconds}s";
    }

    /// <summary>The mockup's fmtDur: 2:14, 1:02:05.</summary>
    public static string Running(TimeSpan time)
    {
        var seconds = Math.Max(0, (long)Math.Floor(time.TotalSeconds));
        var hours = seconds / 3600;
        var minutes = seconds % 3600 / 60;
        var rest = (seconds % 60).ToString("00", CultureInfo.InvariantCulture);
        return hours > 0 ? $"{hours}:{minutes.ToString("00", CultureInfo.InvariantCulture)}:{rest}" : $"{minutes}:{rest}";
    }

    /// <summary>The Claude icon on the left of the bar (mockup renderClLauncher, F-LEFT-05).</summary>
    public static string LauncherTooltip(IReadOnlyList<ClaudeSession> sessions)
    {
        var ask = sessions.Count(s => s.Status == ClaudeStatus.NeedsInput);
        var work = sessions.Count(s => s.Status == ClaudeStatus.Working);
        return ask > 0 ? $"Claude · {ask} need{(ask > 1 ? "" : "s")} input"
            : work > 0 ? $"Claude · {work} working"
            : "Claude sessions";
    }

    public static string AppName(ClaudeApp app) => app == ClaudeApp.Desktop ? "Claude Desktop" : "Terminal";

    private static string Tooltip(ClaudeSession s, bool snippet) =>
        $"{s.Name}\n{s.Cwd} · {AppName(s.App)}" + (snippet && !string.IsNullOrEmpty(s.Snippet) ? "\n" + s.Snippet : "");

    /// <summary>The mockup's trunc(): the first n − 1 characters and an ellipsis.</summary>
    private static string Trunc(string text) => text.Length > NameLength ? text[..(NameLength - 1)] + "…" : text;
}
