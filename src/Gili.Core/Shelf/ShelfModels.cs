namespace Gili.Core.Shelf;

/// <summary>
/// One drop on the Shelf (PRD 6.3 "Shelf"): a file or folder, or a bundle of everything dropped together. Only the
/// paths are kept, never a copy.
/// </summary>
public sealed record ShelfItem(long Id, IReadOnlyList<string> Paths, string DisplayName, long SizeBytes, int SortOrder, DateTimeOffset AddedAt)
{
    public int FileCount => Paths.Count;

    public bool IsBundle => Paths.Count > 1;
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
