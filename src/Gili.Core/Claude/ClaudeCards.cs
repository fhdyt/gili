namespace Gili.Core.Claude;

/// <summary>
/// What the "Needs input" card shows (PRD 6.3 "Kartu Needs input", mockup CL_ASK): the title for the kind of wait, the
/// session's name, and the question's first 90 characters or the permission's message. A plan has no third line.
/// </summary>
public sealed record ClaudeCardView(string SessionId, string Title, string Name, string? Quote, bool Terminal)
{
    public string OpenLabel => Terminal ? "Open terminal" : "Open Claude";
}

/// <summary>What the island must do with the card after an update.</summary>
public enum ClaudeCardChange { None, Show, Refresh, Dismiss }

/// <summary>
/// Picks the session the one Claude card stands for (F-CLD-03, mockup clAsk/clCardFor/clCloseCard). Each wait opens the
/// card once: a newer wait takes the card over, and a wait that was carded, closed with Later, timed out or skipped in
/// fullscreen never opens it again. The card goes when its session stops waiting. A pomodoro focus phase holds the card
/// in the IslandCoordinator, which shows it at the break if it is still there.
/// </summary>
public sealed class ClaudeCards
{
    public const string CardId = "claude";
    private const int QuoteLength = 90;

    private readonly HashSet<long> _handled = [];
    private long _wait;

    public ClaudeCardView? Current { get; private set; }

    public ClaudeCardChange Update(IReadOnlyList<ClaudeSession> sessions, bool fullscreen)
    {
        var change = ClaudeCardChange.None;
        if (Current is { } shown)
        {
            var session = sessions.FirstOrDefault(s => s.Id == shown.SessionId);
            if (session is not { Status: ClaudeStatus.NeedsInput } || session.WaitId != _wait)
            {
                Current = null;
                change = ClaudeCardChange.Dismiss;
            }
            else if (View(session) is var view && view != shown)
            {
                // The hook's message came after the session file's wait: same card, fuller text.
                Current = view;
                change = ClaudeCardChange.Refresh;
            }
        }

        var waits = sessions.Where(s => s.Status == ClaudeStatus.NeedsInput).ToList();
        var fresh = waits.Where(s => !_handled.Contains(s.WaitId)).ToList();
        _handled.IntersectWith(waits.Select(s => s.WaitId));
        _handled.UnionWith(fresh.Select(s => s.WaitId));
        // In fullscreen the card is skipped, not kept for later (PRD 6.3: no toast either).
        if (fresh.Count == 0 || fullscreen) return change;
        var newest = fresh.MaxBy(s => s.WaitId)!;
        _wait = newest.WaitId;
        Current = View(newest);
        return ClaudeCardChange.Show;
    }

    /// <summary>Later, Open, or its 8 s are over: the card has gone and its wait stays handled.</summary>
    public void Close() => Current = null;

    private static ClaudeCardView View(ClaudeSession s) => new(s.Id,
        s.Need switch
        {
            ClaudeNeed.Permission => "Claude needs permission",
            ClaudeNeed.Plan => "Plan ready for review",
            _ => "Claude needs your input",
        },
        s.Name,
        s.Need == ClaudeNeed.Plan || string.IsNullOrWhiteSpace(s.Snippet) ? null : Trunc(s.Snippet.Trim()),
        s.App == ClaudeApp.Terminal);

    private static string Trunc(string text) => text.Length > QuoteLength ? text[..(QuoteLength - 1)] + "…" : text;
}
