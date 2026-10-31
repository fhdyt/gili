namespace Gili.Core.Shelf;

/// <summary>
/// The drop zone's two columns (F-ISL-30): Copy leaves the file where it is when dragged out, Cut moves it there.
/// </summary>
public enum ShelfMode { Copy, Cut }

/// <summary>
/// One drop on the Shelf (PRD 6.3 "Shelf"): a file or folder, or a bundle of everything dropped together. Usually
/// only the paths are kept; files from inside a zip or the temp folder are copied into <paramref name="Folder"/>, a
/// folder of Gili's own that goes with the item (F-ISL-31).
/// </summary>
public sealed record ShelfItem(
    long Id,
    IReadOnlyList<string> Paths,
    string DisplayName,
    long SizeBytes,
    int SortOrder,
    DateTimeOffset AddedAt,
    ShelfMode Mode = ShelfMode.Copy,
    string? Folder = null)
{
    public int FileCount => Paths.Count;

    public bool IsBundle => Paths.Count > 1;

    /// <summary>Gili holds the files itself, in <see cref="Folder"/>.</summary>
    public bool IsKept => Folder is not null;
}

/// <summary>What the disk says about a path that is still there; a folder's size is not read (plan 3d).</summary>
public readonly record struct FileFacts(bool IsFolder, long Size);

/// <summary>The <c>shelf_items</c> table (PRD 10).</summary>
public interface IShelfStore
{
    IReadOnlyList<ShelfItem> All();

    /// <summary>Saves a new item and returns it with its id.</summary>
    ShelfItem Add(ShelfItem item);

    void Update(ShelfItem item);

    void Delete(long id);

    void Clear();
}
