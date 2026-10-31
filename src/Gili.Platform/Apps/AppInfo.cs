using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using static Gili.Platform.Interop.ShellInterop;

namespace Gili.Platform.Apps;

/// <summary>An app icon as premultiplied 32-bit BGRA pixels, top row first.</summary>
public sealed record IconPixels(int Width, int Height, byte[] Bgra);

/// <summary>
/// An app's name and its own icon (PRD 9.2): Store and pinned apps by their AppUserModelID through the shell's Apps
/// folder, desktop apps from their executable. The app caches the result per app.
/// </summary>
public static class AppInfo
{
    public static (string Name, IconPixels? Icon) Resolve(string appKey, nint sampleWindow, int pixelSize)
    {
        string? name = null;
        IconPixels? icon = null;
        if (appKey.StartsWith("aumid:", StringComparison.Ordinal) && Item(@"shell:AppsFolder\" + appKey[6..]) is { } app)
        {
            name = DisplayName(app);
            icon = Image(app, pixelSize);
            Marshal.ReleaseComObject(app);
        }
        var path = appKey.StartsWith("exe:", StringComparison.Ordinal) ? appKey[4..] : TaskbarWindows.ProcessPath(sampleWindow);
        if (path is not null && (name is null || icon is null))
        {
            name ??= Describe(path);
            if (icon is null && Item(path) is { } file)
            {
                icon = Image(file, pixelSize);
                Marshal.ReleaseComObject(file);
            }
        }
        return (name ?? "App", icon);
    }

    /// <summary>Only the app's name, as the bar's strip shows it (Windows Terminal, Claude).</summary>
    public static string Name(string appKey, nint sampleWindow)
    {
        if (appKey.StartsWith("aumid:", StringComparison.Ordinal) && Item(@"shell:AppsFolder\" + appKey[6..]) is { } app)
        {
            var name = DisplayName(app);
            Marshal.ReleaseComObject(app);
            if (name is not null) return name;
        }
        var path = appKey.StartsWith("exe:", StringComparison.Ordinal) ? appKey[4..] : TaskbarWindows.ProcessPath(sampleWindow);
        return path is null ? "App" : Describe(path);
    }

    /// <summary>The file description ("Google Chrome"), or the file name without .exe.</summary>
    private static string Describe(string path)
    {
        try
        {
            var description = FileVersionInfo.GetVersionInfo(path).FileDescription;
            if (!string.IsNullOrWhiteSpace(description)) return description.Trim();
        }
        catch (FileNotFoundException)
        {
        }
        return Path.GetFileNameWithoutExtension(path);
    }

    private static IShellItem? Item(string parsingName)
    {
        var iid = typeof(IShellItem).GUID;
        return SHCreateItemFromParsingName(parsingName, 0, ref iid, out var item) == 0 ? item : null;
    }

    private static string? DisplayName(IShellItem item)
    {
        if (item.GetDisplayName(SIGDN_NORMALDISPLAY, out var pointer) != 0 || pointer == 0) return null;
        try
        {
            return Marshal.PtrToStringUni(pointer);
        }
        finally
        {
            CoTaskMemFree(pointer);
        }
    }

    /// <summary>The item's icon at the pixel size, read out of the HBITMAP the shell hands back.</summary>
    private static IconPixels? Image(IShellItem item, int size)
    {
        if (item is not IShellItemImageFactory factory) return null;
        if (factory.GetImage(new SIZE { cx = size, cy = size }, SIIGBF_ICONONLY, out var bitmap) != 0 || bitmap == 0) return null;
        try
        {
            if (GetObject(bitmap, Marshal.SizeOf<BITMAP>(), out var info) == 0) return null;
            var header = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = info.bmWidth,
                biHeight = -info.bmHeight,
                biPlanes = 1,
                biBitCount = 32,
            };
            var pixels = new byte[info.bmWidth * info.bmHeight * 4];
            var dc = GetDC(0);
            try
            {
                if (GetDIBits(dc, bitmap, 0, (uint)info.bmHeight, pixels, ref header, 0) == 0) return null;
            }
            finally
            {
                ReleaseDC(0, dc);
            }
            return new IconPixels(info.bmWidth, info.bmHeight, pixels);
        }
        finally
        {
            DeleteObject(bitmap);
        }
    }
}
