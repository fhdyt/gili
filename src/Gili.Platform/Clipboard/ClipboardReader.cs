using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Gili.Core.Clipboard;

namespace Gili.Platform.Clipboard;

/// <summary>
/// Reads what a copy put on the clipboard, enough for the island's snippet (PRD 6.3). A copy the source app marked
/// sensitive is not read at all. Text is read up to 4 096 characters and HTML up to 64 KB; nothing is kept or logged.
/// </summary>
public static class ClipboardReader
{
    private const uint CF_UNICODETEXT = 13;
    private const uint CF_HDROP = 15;
    private const uint CF_DIB = 8;
    private const uint CF_DIBV5 = 17;
    private const int DROPEFFECT_MOVE = 2;
    private const int MaxText = 4096;

    private static readonly uint Exclude = RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");
    private static readonly uint HistoryAllowed = RegisterClipboardFormat("CanIncludeInClipboardHistory");
    private static readonly uint DropEffect = RegisterClipboardFormat("Preferred DropEffect");
    private static readonly uint Html = RegisterClipboardFormat("HTML Format");
    private static readonly uint Url = RegisterClipboardFormat("UniformResourceLocatorW");

    /// <summary>Null while another app holds the clipboard open; try again a moment later.</summary>
    public static ClipboardContent? TryRead()
    {
        if (!OpenClipboard(IntPtr.Zero)) return null;
        try
        {
            var owner = OwnerProcess();
            if (IsClipboardFormatAvailable(Exclude) || (IsClipboardFormatAvailable(HistoryAllowed) && ReadDword(HistoryAllowed) == 0))
                return new ClipboardContent(true, null, [], false, false, 0, 0, null, null, owner);
            var files = ReadFiles();
            var (width, height) = ImageSize();
            return new ClipboardContent(
                Sensitive: false,
                // The snippet shows the first line and its tooltip the first 120 characters.
                Text: ReadUnicode(CF_UNICODETEXT, MaxText),
                Files: files,
                Cut: files.Count > 0 && IsClipboardFormatAvailable(DropEffect) && (ReadDword(DropEffect) & DROPEFFECT_MOVE) != 0,
                HasImage: width > 0,
                ImageWidth: width,
                ImageHeight: height,
                Html: ReadUtf8(Html, 64 * 1024),
                Url: ReadUnicode(Url, 2048),
                OwnerProcess: owner);
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static string? OwnerProcess()
    {
        var owner = GetClipboardOwner();
        if (owner == IntPtr.Zero) return null;
        GetWindowThreadProcessId(owner, out var pid);
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static List<string> ReadFiles()
    {
        var files = new List<string>();
        var drop = IsClipboardFormatAvailable(CF_HDROP) ? GetClipboardData(CF_HDROP) : IntPtr.Zero;
        if (drop == IntPtr.Zero) return files;
        var count = DragQueryFile(drop, uint.MaxValue, null, 0);
        for (uint i = 0; i < count; i++)
        {
            var length = DragQueryFile(drop, i, null, 0);
            var name = new StringBuilder((int)length + 1);
            DragQueryFile(drop, i, name, length + 1);
            files.Add(name.ToString());
        }
        return files;
    }

    /// <summary>The bitmap's size from its BITMAPINFOHEADER, without copying the pixels.</summary>
    private static (int Width, int Height) ImageSize()
    {
        var format = IsClipboardFormatAvailable(CF_DIBV5) ? CF_DIBV5 : IsClipboardFormatAvailable(CF_DIB) ? CF_DIB : 0;
        if (format == 0) return (0, 0);
        return WithLocked(format, (ptr, size) => size < 12 ? (0, 0) : (Marshal.ReadInt32(ptr, 4), Math.Abs(Marshal.ReadInt32(ptr, 8))));
    }

    private static int ReadDword(uint format) => WithLocked(format, (ptr, size) => size < 4 ? -1 : Marshal.ReadInt32(ptr));

    private static string? ReadUnicode(uint format, int maxChars) =>
        !IsClipboardFormatAvailable(format) ? null : WithLocked(format, (ptr, size) =>
        {
            if (size < 2) return null;
            var text = Marshal.PtrToStringUni(ptr, (int)Math.Min(size / 2, (nuint)maxChars));
            var end = text.IndexOf('\0');
            return end >= 0 ? text[..end] : text;
        });

    private static string? ReadUtf8(uint format, int maxBytes) =>
        !IsClipboardFormatAvailable(format) ? null : WithLocked(format, (ptr, size) =>
        {
            if (size == 0) return null;
            var bytes = new byte[(int)Math.Min(size, (nuint)maxBytes)];
            Marshal.Copy(ptr, bytes, 0, bytes.Length);
            return Encoding.UTF8.GetString(bytes).TrimEnd('\0');
        });

    /// <summary>Runs <paramref name="read"/> on the format's locked memory; a missing handle reads as size 0.</summary>
    private static T WithLocked<T>(uint format, Func<IntPtr, nuint, T> read)
    {
        var handle = GetClipboardData(format);
        var ptr = handle == IntPtr.Zero ? IntPtr.Zero : GlobalLock(handle);
        if (ptr == IntPtr.Zero) return read(IntPtr.Zero, 0);
        try
        {
            return read(ptr, GlobalSize(handle));
        }
        finally
        {
            GlobalUnlock(handle);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool OpenClipboard(IntPtr owner);

    [DllImport("user32.dll")]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardData(uint format);

    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardOwner();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterClipboardFormat(string name);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint DragQueryFile(IntPtr drop, uint index, StringBuilder? file, uint length);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr handle);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(IntPtr handle);

    [DllImport("kernel32.dll")]
    private static extern nuint GlobalSize(IntPtr handle);
}
