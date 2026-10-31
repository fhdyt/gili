using Gili.Core;
using Gili.Core.Shelf;

namespace Gili.Platform.Shelf;

/// <summary>
/// Gili's own Shelf folder (F-ISL-31): one subfolder per drop that Gili had to copy, because the files came from
/// inside a zip or from the temp folder and would vanish. Only ever deletes inside its own root.
/// </summary>
public sealed class ShelfFolder(string root)
{
    public string Root { get; } = root;

    /// <summary>A new, empty subfolder for one drop.</summary>
    public string Create()
    {
        var folder = Path.Combine(Root, Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>Deletes a kept item's folder; anything outside the root is left alone.</summary>
    public void Delete(string folder)
    {
        if (!AppPaths.IsUnder(folder, Root)) return;
        try
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A file still open somewhere; the sweep at the next start tries again.
        }
    }

    /// <summary>At start: deletes every subfolder no item uses any more.</summary>
    public void Sweep(IReadOnlyList<string> kept)
    {
        if (!Directory.Exists(Root)) return;
        var keep = kept.Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in Directory.EnumerateDirectories(Root))
        {
            if (!keep.Contains(Path.GetFullPath(folder))) Delete(folder);
        }
    }

    /// <summary>
    /// Copies the dropped paths that live in the temp folder into a new subfolder, on a worker thread, and returns the
    /// paths the item should hold with the folder; the others stay as they are. Null folder when nothing needed copying.
    /// </summary>
    public async Task<(IReadOnlyList<string> Paths, string? Folder)> KeepTempFiles(IReadOnlyList<string> paths, string tempFolder)
    {
        if (!paths.Any(p => ShelfDrop.MustKeep(p, tempFolder))) return (paths, null);
        var folder = Create();
        var result = await Task.Run(() => paths.Select(p => ShelfDrop.MustKeep(p, tempFolder) ? CopyInto(p, folder) : p).ToList());
        return (result, folder);
    }

    private static string CopyInto(string source, string folder)
    {
        var target = Path.Combine(folder, Path.GetFileName(source.TrimEnd('\\', '/')));
        try
        {
            if (Directory.Exists(source)) CopyDirectory(source, target);
            else File.Copy(source, target, overwrite: true);
            return target;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Couldn't copy (gone already, locked): keep pointing at the original, as before.
            return source;
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
        foreach (var sub in Directory.EnumerateDirectories(source)) CopyDirectory(sub, Path.Combine(target, Path.GetFileName(sub)));
    }
}
