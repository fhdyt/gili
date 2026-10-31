using System.IO;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Gili.App.Island;

/// <summary>
/// The clipboard history's images (F-ISL-32): PNGs in Gili's clipboard folder, each named by a hash of its pixels so
/// copying the same image again finds the same file. Only ever deletes plain file names inside its own folder.
/// </summary>
public sealed class ClipboardImages(string root)
{
    /// <summary>Larger copies stay a snippet only, so one huge image can't fill the disk or stall the encoder.</summary>
    public const long MaxPixels = 25_000_000;

    /// <summary>The list's thumbnails are decoded this wide, twice the row's 40 px for high DPI.</summary>
    private const int ThumbWidth = 80;

    public string Root { get; } = root;

    /// <summary>
    /// Saves the copied bitmap as a PNG on a worker thread and returns its file name; null when it is too large or
    /// can't be written. The bitmap must be frozen.
    /// </summary>
    public Task<string?> Save(BitmapSource image) => Task.Run(() =>
    {
        if ((long)image.PixelWidth * image.PixelHeight > MaxPixels) return null;
        try
        {
            var source = image.Format == PixelFormats.Bgra32 || image.Format == PixelFormats.Bgr32 ? image : new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
            var stride = source.PixelWidth * 4;
            var pixels = new byte[stride * source.PixelHeight];
            source.CopyPixels(pixels, stride, 0);
            var name = Convert.ToHexString(SHA1.HashData(pixels))[..16].ToLowerInvariant() + ".png";
            var path = Path.Combine(Root, name);
            if (File.Exists(path)) return name;
            Directory.CreateDirectory(Root);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            var temp = path + ".tmp";
            using (var file = File.Create(temp)) encoder.Save(file);
            File.Move(temp, path, overwrite: true);
            return name;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException)
        {
            return null;
        }
    });

    /// <summary>The full image, for pasting it again; null when the file is gone.</summary>
    public BitmapSource? Load(string name) => Decode(name, 0);

    /// <summary>A small copy for the list row.</summary>
    public BitmapSource? Thumb(string name) => Decode(name, ThumbWidth);

    public void Delete(string name)
    {
        if (!IsPlainName(name)) return;
        try
        {
            File.Delete(Path.Combine(Root, name));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Still open somewhere; the sweep at the next start tries again.
        }
    }

    /// <summary>At start: deletes every PNG no row uses any more.</summary>
    public void Sweep(IReadOnlyList<string> inUse)
    {
        if (!Directory.Exists(Root)) return;
        var keep = inUse.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(Root))
        {
            var name = Path.GetFileName(file);
            if (!keep.Contains(name)) Delete(name);
        }
    }

    private BitmapSource? Decode(string name, int width)
    {
        if (!IsPlainName(name)) return null;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(Path.Combine(Root, name));
            if (width > 0) image.DecodePixelWidth = width;
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

    private static bool IsPlainName(string name) => name.Length > 0 && name == Path.GetFileName(name);
}
