using System.Text.RegularExpressions;

namespace Gili.Core.Clipboard;

/// <summary>One copied text in the history; <paramref name="CopiedAt"/> is UTC.</summary>
public sealed record ClipboardEntry(long Id, string Text, string? SourceApp, DateTimeOffset CopiedAt);

/// <summary>The clipboard_history table (PRD 10).</summary>
public interface IClipboardHistoryStore
{
    /// <summary>Every entry, newest first.</summary>
    IReadOnlyList<ClipboardEntry> All();

    /// <summary>Inserts an entry with Id 0, else updates it; returns it with its id.</summary>
    ClipboardEntry Save(ClipboardEntry entry);

    void Delete(long id);

    void Clear();
}

/// <summary>
/// The last 50 texts copied, newest first (F-LEFT-03, PRD 6.3 "Halaman Clipboard"). The same text copied again moves to
/// the top instead of becoming a second row; past 50 the oldest goes.
/// </summary>
public sealed class ClipboardHistory
{
    public const int Max = 50;

    /// <summary>Longer copies are kept only up to here, so one huge copy can't swell the database (design 4a).</summary>
    public const int MaxLength = 100_000;

    private readonly IClipboardHistoryStore? _store;
    private readonly Func<DateTimeOffset> _now;
    private readonly List<ClipboardEntry> _items;

    public ClipboardHistory(IClipboardHistoryStore? store, Func<DateTimeOffset> now)
    {
        _store = store;
        _now = now;
        _items = [.. (store?.All() ?? []).OrderByDescending(e => e.CopiedAt).Take(Max)];
    }

    public IReadOnlyList<ClipboardEntry> Items => _items;

    public event Action? Changed;

    /// <summary>A text was copied; blank text is ignored.</summary>
    public void Add(string text, string? sourceApp)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (text.Length > MaxLength) text = text[..MaxLength];
        var same = _items.FindIndex(e => e.Text == text);
        var entry = same >= 0 ? _items[same] with { SourceApp = sourceApp ?? _items[same].SourceApp, CopiedAt = _now() }
            : new ClipboardEntry(0, text, sourceApp, _now());
        if (same >= 0) _items.RemoveAt(same);
        _items.Insert(0, _store?.Save(entry) ?? entry with { Id = NextId() });
        while (_items.Count > Max)
        {
            _store?.Delete(_items[^1].Id);
            _items.RemoveAt(_items.Count - 1);
        }
        Changed?.Invoke();
    }

    /// <summary>A row was pasted: it moves to the top, as if copied now.</summary>
    public void Use(long id)
    {
        var index = _items.FindIndex(e => e.Id == id);
        if (index < 0) return;
        var entry = _items[index] with { CopiedAt = _now() };
        _items.RemoveAt(index);
        _items.Insert(0, _store?.Save(entry) ?? entry);
        Changed?.Invoke();
    }

    public void Remove(long id)
    {
        if (_items.RemoveAll(e => e.Id == id) == 0) return;
        _store?.Delete(id);
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (_items.Count == 0) return;
        _items.Clear();
        _store?.Clear();
        Changed?.Invoke();
    }

    /// <summary>The entries whose text contains the query, ignoring case; all of them for an empty query.</summary>
    public IReadOnlyList<ClipboardEntry> Search(string? query)
    {
        var q = query?.Trim();
        return string.IsNullOrEmpty(q) ? _items : [.. _items.Where(e => e.Text.Contains(q, StringComparison.CurrentCultureIgnoreCase))];
    }

    private long NextId() => _items.Count == 0 ? 1 : Math.Max(1, _items.Max(e => e.Id) + 1);
}

/// <summary>How a history row reads (mockup renderClipPage, ago).</summary>
public static partial class ClipboardText
{
    public const int TooltipLength = 400;

    /// <summary>Spaces and line breaks squeezed to one space, trimmed.</summary>
    public static string Line(string text) => Spaces().Replace(text, " ").Trim();

    /// <summary>now, 38 min, 2 h, Yesterday, 3 days.</summary>
    public static string Ago(DateTimeOffset at, DateTimeOffset now)
    {
        var minutes = (int)Math.Floor((now - at).TotalMinutes);
        return minutes < 1 ? "now"
            : minutes < 60 ? $"{minutes} min"
            : minutes < 1440 ? $"{minutes / 60} h"
            : minutes < 2880 ? "Yesterday"
            : $"{minutes / 1440} days";
    }

    /// <summary>"Chrome · 38 min", or only the time when the app is unknown.</summary>
    public static string Detail(ClipboardEntry entry, DateTimeOffset now) =>
        string.IsNullOrEmpty(entry.SourceApp) ? Ago(entry.CopiedAt, now) : $"{entry.SourceApp} · {Ago(entry.CopiedAt, now)}";

    public static string Tooltip(string text) => text.Length > TooltipLength ? text[..TooltipLength] : text;

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
