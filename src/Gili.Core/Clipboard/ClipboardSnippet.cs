using System.Globalization;
using System.Text.RegularExpressions;

namespace Gili.Core.Clipboard;

public enum SnippetKind { Text, Image, Files, Cut, Hidden }

/// <summary>
/// What the clipboard holds after a copy, as read by Platform. Nothing of a sensitive copy is read: only
/// <paramref name="Sensitive"/> is set (PRD 6.3). <paramref name="OwnerWindow"/> is the copying app's clipboard window,
/// for the history's app name.
/// </summary>
public sealed record ClipboardContent(
    bool Sensitive,
    string? Text,
    IReadOnlyList<string> Files,
    bool Cut,
    bool HasImage,
    int ImageWidth,
    int ImageHeight,
    string? Html,
    string? Url,
    string? OwnerProcess,
    nint OwnerWindow = 0);

/// <summary>The island slot's line for a copy; <paramref name="Detail"/> is an image's size, shown grey.</summary>
public sealed record ClipboardSnippet(SnippetKind Kind, string Text, string? Detail, string Tooltip);

/// <summary>Turns a copy into the slot's snippet (PRD 6.3 "Cuplikan clipboard", mockup <c>ACT.copy*</c>).</summary>
public static partial class ClipboardSnippets
{
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(3);

    private static readonly string[] ScreenshotTools = ["SnippingTool", "ScreenClippingHost", "ScreenSketch"];

    /// <summary>
    /// Files come first, then text, then a bare image: apps like Excel put a picture next to copied text, and the text
    /// is what the user copied. Null when there is nothing to show.
    /// </summary>
    public static ClipboardSnippet? For(ClipboardContent c, Func<string, (int W, int H)?> imageFileSize)
    {
        if (c.Sensitive) return new(SnippetKind.Hidden, "Copied (hidden)", null, "Hidden because the app marked it as sensitive");
        if (c.Files.Count > 0) return ForFiles(c, imageFileSize);
        if (!string.IsNullOrWhiteSpace(c.Text))
        {
            var line = c.Text.Trim().Split('\n')[0].TrimEnd('\r');
            return new(SnippetKind.Text, "\"" + Trunc(line, 40) + "\"", null, "Copied: " + Trunc(c.Text.Trim(), 120));
        }
        if (c.HasImage) return Image(ImageName(c), c.ImageWidth, c.ImageHeight);
        return null;
    }

    /// <summary>From the file, the image's URL, a screenshot tool, or nothing known (PRD 6.3, plan 1i decision 2).</summary>
    public static string ImageName(ClipboardContent c)
    {
        if (c.Files.Count == 1) return Path.GetFileName(c.Files[0]);
        if (NameFromUrl(c.Url) is { } fromUrl) return fromUrl;
        if (c.Html is not null && ImageSource().Match(c.Html) is { Success: true } m && NameFromUrl(m.Groups[1].Value) is { } fromHtml) return fromHtml;
        if (c.OwnerProcess is null || ScreenshotTools.Contains(c.OwnerProcess, StringComparer.OrdinalIgnoreCase)) return "Screenshot";
        return "Image";
    }

    private static ClipboardSnippet ForFiles(ClipboardContent c, Func<string, (int W, int H)?> imageFileSize)
    {
        var names = c.Files.Select(Path.GetFileName).ToList();
        var list = string.Join(", ", names);
        if (c.Cut) return new(SnippetKind.Cut, Count(names), null, "Cut: " + list);
        if (names.Count == 1 && imageFileSize(c.Files[0]) is { } size) return Image(names[0]!, size.W, size.H);
        return new(SnippetKind.Files, Count(names), null, "Copied: " + list);
    }

    private static string Count(List<string?> names) =>
        names.Count == 1 ? Trunc(names[0]!, 32) : names.Count.ToString(CultureInfo.InvariantCulture) + " files";

    private static ClipboardSnippet Image(string name, int width, int height)
    {
        var size = string.Create(CultureInfo.InvariantCulture, $"{width} × {height}");
        return new(SnippetKind.Image, Trunc(name, 32), size, $"Image copied · {name} · {size}");
    }

    private static string? NameFromUrl(string? url)
    {
        if (url is null || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return null;
        var name = Uri.UnescapeDataString(uri.Segments[^1]).Trim('/');
        return name.Length > 0 ? name : null;
    }

    /// <summary>The mockup's trunc(): more than n characters keeps n − 1 and adds an ellipsis.</summary>
    private static string Trunc(string s, int n) => s.Length > n ? s[..(n - 1)] + "…" : s;

    [GeneratedRegex("<img[^>]+src=\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex ImageSource();
}
