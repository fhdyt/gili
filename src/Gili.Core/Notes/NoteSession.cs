namespace Gili.Core.Notes;

/// <summary>
/// The typing pause before a note is written (F-TOOL-09): each key moves the save to 0.5 s after it, and closing the
/// editor, the island or the app takes what is still waiting right away. The caller owns the one timer to <see cref="DueAt"/>.
/// </summary>
public sealed class NoteSession
{
    public static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(500);

    private readonly List<long> _order = [];
    private readonly Dictionary<long, string> _pending = [];

    /// <summary>When the waiting text should be written: the last key plus 0.5 s; null when nothing waits.</summary>
    public DateTimeOffset? DueAt { get; private set; }

    public bool IsPending => _pending.Count > 0;

    public void Edit(long id, string body, DateTimeOffset now)
    {
        if (!_pending.ContainsKey(id)) _order.Add(id);
        _pending[id] = body;
        DueAt = now + Pause;
    }

    /// <summary>Every note still waiting with its latest text, in the order they were first typed in; nothing waits afterwards.</summary>
    public IReadOnlyList<(long Id, string Body)> Take()
    {
        var taken = _order.Select(id => (id, _pending[id])).ToList();
        _order.Clear();
        _pending.Clear();
        DueAt = null;
        return taken;
    }

    /// <summary>A note closed with no text is dropped, new or old (plan 2f decision 4).</summary>
    public static bool Discards(string body) => NoteText.IsBlank(body);
}
