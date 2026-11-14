namespace Gili.Core.Claude;

/// <summary>What the "Claude is done" card shows (F-CLD-09, mockup CARDS.claudeDone): the session and how long it took.</summary>
public sealed record ClaudeDoneView(string SessionId, string Name, string? Quote)
{
    public string Title => "Claude is done";
}

/// <summary>
/// Picks the finish the one "Claude is done" card stands for (F-CLD-09, revision 25). A session that finishes while its
/// window is not in front opens the card once, like a finished timer: it stays until Close or Open Claude, or until the
/// finish is seen some other way (its window comes forward, its bubble is clicked, a new prompt starts) or the session
/// goes. A newer finish takes the card over. In fullscreen the finish is skipped, not kept for later.
/// </summary>
public sealed class ClaudeDoneCards
{
    public const string CardId = "claude-done";

    private readonly HashSet<(string Id, DateTimeOffset At)> _handled = [];
    private DateTimeOffset _finished;

    public ClaudeDoneView? Current { get; private set; }

    public ClaudeCardChange Update(IReadOnlyList<ClaudeSession> sessions, bool fullscreen)
    {
        var change = ClaudeCardChange.None;
        if (Current is { } shown)
        {
            var session = sessions.FirstOrDefault(s => s.Id == shown.SessionId);
            if (session is null || !IsUnseenFinish(session) || session.FinishedAt != _finished)
            {
                Current = null;
                change = ClaudeCardChange.Dismiss;
            }
        }

        var finishes = sessions.Where(IsUnseenFinish).ToList();
        var fresh = finishes.Where(s => !_handled.Contains(Key(s))).ToList();
        _handled.IntersectWith(finishes.Select(Key));
        _handled.UnionWith(fresh.Select(Key));
        if (fresh.Count == 0 || fullscreen) return change;
        var newest = fresh.MaxBy(s => s.FinishedAt)!;
        _finished = newest.FinishedAt!.Value;
        Current = new ClaudeDoneView(newest.Id, newest.Name, newest.DoneAfter is { } took ? "Finished in " + ClaudeSlot.Took(took) : null);
        return ClaudeCardChange.Show;
    }

    /// <summary>Close, Open Claude, or a Needs input card took its place: the card has gone and its finish stays handled.</summary>
    public void Close() => Current = null;

    private static bool IsUnseenFinish(ClaudeSession s) => s.Status == ClaudeStatus.Done && s.Unseen && s.FinishedAt is not null;

    private static (string, DateTimeOffset) Key(ClaudeSession s) => (s.Id, s.FinishedAt!.Value);
}
