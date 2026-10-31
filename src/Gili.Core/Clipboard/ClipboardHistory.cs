using System.Globalization;
using System.Text.RegularExpressions;

namespace Gili.Core.Clipboard;

/// <summary>What a history row holds (F-ISL-32): copied text, a copied image, or copied files.</summary>
public enum ClipKind { Text, Image, Files }

/// <summary>
/// One row of the history; <paramref name="CopiedAt"/> is UTC. <paramref name="Text"/> is the text itself, the image's
/// PNG file name in Gili's clipboard folder, or the files' paths one per line. An image also has its name (Screenshot,
/// photo.jpg) and size.
/// </summary>
public sealed record ClipboardEntry(
    long Id,
    string Text,
    string? SourceApp,
    DateTimeOffset CopiedAt,
    ClipKind Kind = ClipKind.Text,
    string? Name = null,
    int Width = 0,
    int Height = 0)
{
    /// <summary>The copied files' paths; empty unless <see cref="Kind"/> is Files.</summary>
    public IReadOnlyList<string> Files => Kind == ClipKind.Files ? Text.Split('\n', StringSplitOptions.RemoveEmptyEntries) : [];
}

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
/// The last 50 copies, newest first (F-LEFT-03, PRD 6.3 "Halaman Clipboard"): text, images and files (F-ISL-32). The
/// same copy again moves to the top instead of becoming a second row; past 50 the oldest goes, and past 20 images the
/// oldest image. An image that leaves takes its PNG with it.
/// </summary>
public sealed class ClipboardHistory
{
    public const int Max = 50;

    /// <summary>Images kept at most, so the PNGs on disk stay few.</summary>
    public const int MaxImages = 20;

    /// <summary>Longer copies are kept only up to here, so one huge copy can't swell the database (design 4a).</summary>
    public const int MaxLength = 100_000;

    private readonly IClipboardHistoryStore? _store;
    private readonly Func<DateTimeOffset> _now;
    private readonly Action<string>? _discardImage;
    private readonly List<ClipboardEntry> _items;

    /// <param name="discardImage">Deletes an image's PNG once its row is gone.</param>
    public ClipboardHistory(IClipboardHistoryStore? store, Func<DateTimeOffset> now, Action<string>? discardImage = null)
    {
        _store = store;
        _now = now;
        _discardImage = discardImage;
        _items = [.. (store?.All() ?? []).OrderByDescending(e => e.CopiedAt).Take(Max)];
    }

    public IReadOnlyList<ClipboardEntry> Items => _items;

    /// <summary>The PNGs the rows still use; any other file in the clipboard folder is left over.</summary>
    public IReadOnlyList<string> ImageFiles => [.. _items.Where(e => e.Kind == ClipKind.Image).Select(e => e.Text)];

    public event Action? Changed;

    /// <summary>A text was copied; blank text is ignored.</summary>
    public void Add(string text, string? sourceApp)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (text.Length > MaxLength) text = text[..MaxLength];
        Put(new ClipboardEntry(0, text, sourceApp, _now()));
    }

    /// <summary>An image was copied and saved as <paramref name="file"/> (named by its pixels, so the same image matches).</summary>
    public void AddImage(string file, string name, int width, int height, string? sourceApp) =>
        Put(new ClipboardEntry(0, file, sourceApp, _now(), ClipKind.Image, name, width, height));

    /// <summary>Files were copied or cut; the history keeps their paths.</summary>
    public void AddFiles(IReadOnlyList<string> paths, string? sourceApp)
    {
        if (paths.Count == 0) return;
        Put(new ClipboardEntry(0, string.Join('\n', paths), sourceApp, _now(), ClipKind.Files));
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
        if (_items.Find(e => e.Id == id) is not { } entry) return;
        _items.Remove(entry);
        _store?.Delete(id);
        Discard(entry);
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (_items.Count == 0) return;
        var gone = _items.ToList();
        _items.Clear();
        _store?.Clear();
        foreach (var entry in gone) Discard(entry);
        Changed?.Invoke();
    }

    /// <summary>Drops the file rows whose files are all gone; true when the history changed.</summary>
    public bool PruneFiles(Func<string, bool> exists)
    {
        var gone = _items.Where(e => e.Kind == ClipKind.Files && !e.Files.Any(exists)).ToList();
        if (gone.Count == 0) return false;
        foreach (var entry in gone)
        {
            _items.Remove(entry);
            _store?.Delete(entry.Id);
        }
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// The entries that match the query, ignoring case: text by its words, files by their names, images by their name;
    /// all of them for an empty query.
    /// </summary>
    public IReadOnlyList<ClipboardEntry> Search(string? query)
    {
        var q = query?.Trim();
        return string.IsNullOrEmpty(q) ? _items : [.. _items.Where(e => Searchable(e).Contains(q, StringComparison.CurrentCultureIgnoreCase))];
    }

    private static string Searchable(ClipboardEntry e) => e.Kind switch
    {
        ClipKind.Image => e.Name ?? "",
        ClipKind.Files => string.Join('\n', e.Files.Select(p => Path.GetFileName(p.TrimEnd('\\', '/')))),
        _ => e.Text,
    };

    /// <summary>Puts a new copy on top, or moves the same one up; then trims to 20 images and 50 rows.</summary>
    private void Put(ClipboardEntry fresh)
    {
        var same = _items.FindIndex(e => e.Kind == fresh.Kind && e.Text == fresh.Text);
        var entry = same >= 0
            ? _items[same] with { SourceApp = fresh.SourceApp ?? _items[same].SourceApp, CopiedAt = fresh.CopiedAt, Name = fresh.Name ?? _items[same].Name }
            : fresh;
        if (same >= 0) _items.RemoveAt(same);
        _items.Insert(0, _store?.Save(entry) ?? entry with { Id = entry.Id != 0 ? entry.Id : NextId() });
        while (_items.Count(e => e.Kind == ClipKind.Image) > MaxImages) Drop(_items.FindLastIndex(e => e.Kind == ClipKind.Image));
        while (_items.Count > Max) Drop(_items.Count - 1);
        Changed?.Invoke();
    }

    private void Drop(int index)
    {
        var entry = _items[index];
        _store?.Delete(entry.Id);
        _items.RemoveAt(index);
        Discard(entry);
    }

    private void Discard(ClipboardEntry entry)
    {
        if (entry.Kind == ClipKind.Image) _discardImage?.Invoke(entry.Text);
    }

    private long NextId() => _items.Count == 0 ? 1 : Math.Max(1, _items.Max(e => e.Id) + 1);
}

/// <summary>How a history row reads (mockup renderClipPage, ago).</summary>
public static partial class ClipboardText
{
    public const int TooltipLength = 400;

    /// <summary>Spaces and line breaks squeezed to one space, trimmed.</summary>
    public static string Line(string text) => Spaces().Replace(text, " ").Trim();

    /// <summary>A row's line: the text, "Screenshot · 1920 × 1080", the file's name, or "3 files" (F-ISL-32).</summary>
    public static string Line(ClipboardEntry entry) => entry.Kind switch
    {
        ClipKind.Image => ImageLine(entry),
        ClipKind.Files => entry.Files.Count == 1 ? Path.GetFileName(entry.Files[0].TrimEnd('\\', '/')) : $"{entry.Files.Count} files",
        _ => Line(entry.Text),
    };

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

    /// <summary>Up to 400 characters of the text or of the files' paths; an image's name and size.</summary>
    public static string Tooltip(ClipboardEntry entry) => entry.Kind == ClipKind.Image ? ImageLine(entry) : Tooltip(entry.Text);

    private static string ImageLine(ClipboardEntry entry) =>
        string.Create(CultureInfo.InvariantCulture, $"{entry.Name ?? "Image"} · {entry.Width} × {entry.Height}");

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
