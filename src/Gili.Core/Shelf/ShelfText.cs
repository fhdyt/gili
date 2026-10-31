using System.Globalization;

namespace Gili.Core.Shelf;

/// <summary>The Shelf's words and numbers as the mockup writes them (<c>renderShelf</c>, <c>fmtSize</c>, <c>FILE_COLORS</c>).</summary>
public static class ShelfText
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private static readonly Dictionary<string, string> Colors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pdf"] = "#FF6961",
        ["jpg"] = "#BF5AF2",
        ["png"] = "#BF5AF2",
        ["zip"] = "#FFD60A",
        ["docx"] = "#64D2FF",
        ["xlsx"] = "#30D158",
        ["mp4"] = "#FF9F0A",
        ["mkv"] = "#FF9F0A",
    };

    private const string Grey = "#8E8E93";
    private const string BundleColor = "#D1D1D6";

    /// <summary>Decimal units, one decimal from MB up, never under 1 KB.</summary>
    public static string Size(long bytes) => bytes switch
    {
        >= 1_000_000_000 => (bytes / 1e9).ToString("0.0", Invariant) + " GB",
        >= 1_000_000 => (bytes / 1e6).ToString("0.0", Invariant) + " MB",
        _ => Math.Max(1, Math.Round(bytes / 1e3, MidpointRounding.AwayFromZero)).ToString("0", Invariant) + " KB",
    };

    public static string Files(int count) => count == 1 ? "1 file" : $"{count} files";

    /// <summary>Under the big count: "3 files · 12.4 MB", or "files on the shelf" when empty.</summary>
    public static string Summary(int files, long bytes) => files == 0 ? "files on the shelf" : $"{Files(files)} · {Size(bytes)}";

    /// <summary>The left icon's tooltip (PRD 6.2).</summary>
    public static string BarTooltip(int files) => files == 0 ? "Shelf" : $"Shelf · {Files(files)}";

    /// <summary>A tile's hover text: one line per file with its size, or "Folder".</summary>
    public static string Tooltip(IEnumerable<(string Path, FileFacts? Facts)> files) =>
        string.Join("\n", files.Select(f => $"{Path.GetFileName(f.Path.TrimEnd('\\', '/'))} · {(f.Facts is { IsFolder: true } ? "Folder" : Size(f.Facts?.Size ?? 0))}"));

    /// <summary>The same, led by what Cut does and followed by a note when Gili keeps the files (F-ISL-30, F-ISL-31).</summary>
    public static string Tooltip(IEnumerable<(string Path, FileFacts? Facts)> files, ShelfMode mode, bool kept)
    {
        var lines = new List<string>();
        if (mode == ShelfMode.Cut) lines.Add("Cut · moves when you drag it out");
        lines.Add(Tooltip(files));
        if (kept) lines.Add("Kept by Gili until you drag it out");
        return string.Join("\n", lines);
    }

    /// <summary>
    /// The badge on a tile's file icon: the extension (at most four letters) in its colour, "FILE" without one, a
    /// bundle's count, and nothing on a folder.
    /// </summary>
    public static (string Text, string Color) Badge(ShelfItem item, bool isFolder)
    {
        if (item.IsBundle) return (item.FileCount.ToString(Invariant), BundleColor);
        if (isFolder) return ("", "");
        var extension = Path.GetExtension(item.Paths[0]).TrimStart('.');
        if (extension.Length == 0) return ("FILE", Grey);
        var text = extension.Length > 4 ? extension[..4] : extension;
        return (text.ToUpperInvariant(), Colors.GetValueOrDefault(extension, Grey));
    }
}
