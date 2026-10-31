namespace Gili.Core.Shelf;

/// <summary>
/// The Shelf (F-ISL-12): one drop is one item, at most eight, kept as references and saved; files that are gone leave
/// the Shelf (a bundle loses only those). An item whose files Gili keeps in a folder of its own takes that folder with
/// it when it leaves (F-ISL-31). Without a database it still works for the session.
/// </summary>
public sealed class ShelfService
{
    public const int MaxItems = 8;

    private readonly IShelfStore? _store;
    private readonly Func<string, FileFacts?> _probe;
    private readonly Action<string>? _discard;
    private readonly List<ShelfItem> _items;
    private ShelfItem? _demo;
    private long _nextLocalId = 1;

    /// <param name="discard">Deletes a kept item's folder once the item is gone.</param>
    public ShelfService(IShelfStore? store, Func<string, FileFacts?> probe, Action<string>? discard = null)
    {
        _store = store;
        _probe = probe;
        _discard = discard;
        _items = [.. store?.All() ?? []];
    }

    public event Action? Changed;

    /// <summary>The demo's item first, while the demo plays, then the saved items in the order they came.</summary>
    public IReadOnlyList<ShelfItem> Items => _demo is null ? _items : [_demo, .. _items];

    public bool IsFull => _items.Count >= MaxItems;

    public int FileCount => Items.Sum(i => i.FileCount);

    public long TotalBytes => Items.Sum(i => i.SizeBytes);

    /// <summary>The folders the saved items keep their files in; any other folder in Gili's Shelf folder is left over.</summary>
    public IReadOnlyList<string> KeptFolders => [.. _items.Where(i => i.IsKept).Select(i => i.Folder!)];

    /// <summary>
    /// Adds what was dropped as one item, in the column it was dropped on, with the folder Gili copied it into, if any;
    /// null when the Shelf is full or nothing dropped is still there.
    /// </summary>
    public ShelfItem? Add(IReadOnlyList<string> paths, DateTimeOffset now, ShelfMode mode = ShelfMode.Copy, string? folder = null)
    {
        if (IsFull) return null;
        var present = paths.Distinct(StringComparer.OrdinalIgnoreCase).Select(p => (Path: p, Facts: _probe(p))).Where(p => p.Facts is not null).ToList();
        if (present.Count == 0) return null;
        var item = Describe(new ShelfItem(0, [.. present.Select(p => p.Path)], "", 0, NextOrder(), now, mode, folder), present.Select(p => p.Facts!.Value));
        item = _store?.Add(item) ?? item with { Id = _nextLocalId++ };
        _items.Add(item);
        Changed?.Invoke();
        return item;
    }

    public void Remove(long id)
    {
        if (_items.Find(i => i.Id == id) is not { } item) return;
        _items.Remove(item);
        _store?.Delete(id);
        Discard(item);
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (_items.Count == 0) return;
        var gone = _items.ToList();
        _items.Clear();
        _store?.Clear();
        foreach (var item in gone) Discard(item);
        Changed?.Invoke();
    }

    /// <summary>Drops every file that is gone; true when the Shelf changed. The demo item is left alone.</summary>
    public bool Prune()
    {
        var changed = false;
        foreach (var item in _items.ToList()) changed |= Check(item);
        if (changed) Changed?.Invoke();
        return changed;
    }

    /// <summary>Checks one item, e.g. after it was dragged out (a move leaves its files gone).</summary>
    public bool PruneItem(long id)
    {
        if (_items.Find(i => i.Id == id) is not { } item || !Check(item)) return false;
        Changed?.Invoke();
        return true;
    }

    /// <summary>The demo's item, shown on top and never saved; null takes it away (F-BAR-09).</summary>
    public void ShowDemo(ShelfItem? item)
    {
        if (_demo == item) return;
        _demo = item;
        Changed?.Invoke();
    }

    /// <summary>What the disk says about a path now: null when it is gone.</summary>
    public FileFacts? Facts(string path) => _probe(path);

    private bool Check(ShelfItem item)
    {
        var present = item.Paths.Select(p => (Path: p, Facts: _probe(p))).Where(p => p.Facts is not null).ToList();
        if (present.Count == item.Paths.Count) return false;
        var index = _items.IndexOf(item);
        if (present.Count == 0)
        {
            _items.RemoveAt(index);
            _store?.Delete(item.Id);
            Discard(item);
            return true;
        }
        var kept = Describe(item with { Paths = [.. present.Select(p => p.Path)] }, present.Select(p => p.Facts!.Value));
        _items[index] = kept;
        _store?.Update(kept);
        return true;
    }

    /// <summary>A bundle is named by its count ("12 files"), a single file or folder by its own name.</summary>
    private static ShelfItem Describe(ShelfItem item, IEnumerable<FileFacts> facts) => item with
    {
        DisplayName = item.Paths.Count == 1 ? Name(item.Paths[0]) : $"{item.Paths.Count} files",
        SizeBytes = facts.Where(f => !f.IsFolder).Sum(f => f.Size),
    };

    private static string Name(string path)
    {
        var trimmed = path.TrimEnd('\\', '/');
        var name = Path.GetFileName(trimmed);
        return name.Length > 0 ? name : trimmed;
    }

    private void Discard(ShelfItem item)
    {
        if (item.Folder is { } folder) _discard?.Invoke(folder);
    }

    private int NextOrder() => _items.Count == 0 ? 0 : _items.Max(i => i.SortOrder) + 1;
}
