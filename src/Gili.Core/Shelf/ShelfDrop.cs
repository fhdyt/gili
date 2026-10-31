namespace Gili.Core.Shelf;

/// <summary>
/// Which dropped files Gili copies into its own Shelf folder (F-ISL-31): files in the temp folder, where 7-Zip, WinRAR
/// and Explorer unpack an archive and delete it again later, and the virtual files a zip folder, Outlook or a browser
/// hands over without a path. Everything else stays a reference.
/// </summary>
public static class ShelfDrop
{
    /// <summary>A drop of virtual files larger than this is refused, so the disk never fills up unnoticed.</summary>
    public const long MaxKeptBytes = 4L * 1024 * 1024 * 1024;

    private static readonly char[] Invalid = [.. Path.GetInvalidFileNameChars().Where(c => c is not ('\\' or '/'))];

    public static bool MustKeep(string path, string tempFolder) => AppPaths.IsUnder(path, tempFolder);

    public static bool FitsLimit(long bytes) => bytes <= MaxKeptBytes;

    /// <summary>
    /// A name from a virtual drop as a path inside the item's folder: no drive, no "..", no characters Windows forbids;
    /// an empty name becomes "File".
    /// </summary>
    public static string SafeRelativePath(string name)
    {
        var parts = name.Replace('/', '\\').Split('\\', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p is not ("." or "..") && !p.EndsWith(':'))
            .Select(p => string.Concat(p.Select(c => Invalid.Contains(c) ? '_' : c)).Trim().TrimEnd('.'))
            .Where(p => p.Length > 0)
            .ToList();
        return parts.Count == 0 ? "File" : string.Join('\\', parts);
    }

    /// <summary>The first part of each relative path, once each, in order: what the item points at.</summary>
    public static IReadOnlyList<string> TopLevel(IEnumerable<string> relativePaths) =>
        [.. relativePaths.Select(p => p.Split('\\')[0]).Distinct(StringComparer.OrdinalIgnoreCase)];
}
