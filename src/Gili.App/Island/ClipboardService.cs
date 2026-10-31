using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Gili.Core.Clipboard;
using Gili.Platform.Clipboard;

namespace Gili.App.Island;

/// <summary>
/// Turns each copy into the island's snippet (F-ISL-17), with a small thumbnail for images. Thumbnails live in memory
/// only and are never written anywhere (PRD 10).
/// </summary>
public sealed class ClipboardService : IDisposable
{
    /// <summary>Thumbnails are decoded at twice the slot's 36 × 20, for high DPI.</summary>
    private const double ThumbWidth = 72;
    private const double ThumbHeight = 40;

    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".ico"];

    private readonly ClipboardMonitor _monitor = new();
    private readonly ClipboardHistory? _history;
    private int _copy;
    private string? _own;
    private DateTimeOffset _ownUntil;

    public ClipboardService(ClipboardHistory? history = null)
    {
        _history = history;
        _monitor.Changed += () => _ = ReadAsync(++_copy);
    }

    /// <summary>
    /// Gili is about to put this text on the clipboard itself (the Clipboard page's paste): that change shows no snippet
    /// and adds no row (PRD 6.3).
    /// </summary>
    public void Ignore(string text)
    {
        _own = text;
        _ownUntil = DateTimeOffset.UtcNow.AddSeconds(3);
    }

    /// <summary>Raised on the UI thread.</summary>
    public event Action<ClipboardSnippet, ImageSource?>? Copied;

    public void Dispose() => _monitor.Dispose();

    /// <summary>
    /// Waits 150 ms first: the update arrives while the copying app may still be flushing its data, and reading then
    /// makes it wait on us and fail its copy. Another app may still hold the clipboard open: try up to 3 more times,
    /// 50 ms apart. A newer copy wins.
    /// </summary>
    private async Task ReadAsync(int copy)
    {
        await Task.Delay(150);
        if (copy != _copy) return;
        var content = ClipboardReader.TryRead();
        for (var attempt = 0; content is null && attempt < 3; attempt++)
        {
            await Task.Delay(50);
            if (copy != _copy) return;
            content = ClipboardReader.TryRead();
        }
        if (content is null) return;
        if (_own is not null && content.Text == _own && DateTimeOffset.UtcNow < _ownUntil)
        {
            _own = null;
            return;
        }
        if (ClipboardSnippets.For(content, ImageFileSize) is not { } snippet) return;
        // Only copied text joins the history (F-LEFT-03); files, images and sensitive copies stay snippets.
        if (snippet.Kind == SnippetKind.Text && content.Text is { } text) _history?.Add(text, ClipboardPaste.SourceApp(content.OwnerWindow));
        var thumb = snippet.Kind != SnippetKind.Image ? null
            : content.Files.Count == 1 ? FileThumb(content.Files[0])
            : ClipboardThumb();
        Copied?.Invoke(snippet, thumb);
    }

    private static (int W, int H)? ImageFileSize(string path)
    {
        if (!ImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)) return null;
        try
        {
            using var stream = File.OpenRead(path);
            var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
            return (frame.PixelWidth, frame.PixelHeight);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException)
        {
            return null;
        }
    }

    private static ImageSource? FileThumb(string path)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path);
            image.DecodePixelHeight = (int)ThumbHeight;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// The copied bitmap scaled to cover the thumbnail and copied into its own small bitmap, so the full image (a 4K
    /// screenshot, say) is not kept alive.
    /// </summary>
    private static ImageSource? ClipboardThumb()
    {
        try
        {
            if (System.Windows.Clipboard.GetImage() is not { PixelWidth: > 0, PixelHeight: > 0 } source) return null;
            var scale = Math.Min(1, Math.Max(ThumbWidth / source.PixelWidth, ThumbHeight / source.PixelHeight));
            var small = new WriteableBitmap(new TransformedBitmap(source, new ScaleTransform(scale, scale)));
            small.Freeze();
            return small;
        }
        catch (COMException)
        {
            return null;
        }
    }
}
