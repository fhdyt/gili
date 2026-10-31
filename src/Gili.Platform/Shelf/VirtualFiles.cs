using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Gili.Core.Shelf;

namespace Gili.Platform.Shelf;

/// <summary>
/// Files dragged without a path (F-ISL-31): a zip folder in Explorer, an Outlook attachment or a browser hands over a
/// <c>FileGroupDescriptorW</c> listing them and one <c>FileContents</c> per file, which must be read while the drop
/// lasts. Contents come as a stream or as memory; storages (Outlook messages) are skipped.
/// </summary>
public static class VirtualFiles
{
    public const string DescriptorFormat = "FileGroupDescriptorW";

    private const int DescriptorSize = 592;
    private const int NameOffset = 72;
    private const uint FdAttributes = 0x04;
    private const uint FdFileSize = 0x40;
    private const uint FileAttributeDirectory = 0x10;
    private const int Chunk = 1 << 20;

    private static readonly short Descriptor = (short)RegisterClipboardFormat(DescriptorFormat);
    private static readonly short Contents = (short)RegisterClipboardFormat("FileContents");

    /// <summary>One listed file or folder, its path relative to the drop and its size when the source says.</summary>
    public sealed record Entry(string RelativePath, bool IsFolder, long Size);

    /// <summary>What the drop lists; null when it has no descriptor or can't be read.</summary>
    public static IReadOnlyList<Entry>? Describe(IDataObject data)
    {
        var format = new FORMATETC { cfFormat = Descriptor, dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = -1, tymed = TYMED.TYMED_HGLOBAL };
        try
        {
            data.GetData(ref format, out var medium);
            try
            {
                return Parse(medium.unionmember);
            }
            finally
            {
                ReleaseStgMedium(ref medium);
            }
        }
        catch (Exception ex) when (ex is COMException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes every listed file into <paramref name="folder"/> and returns the top-level paths the Shelf item holds. A
    /// file whose contents can't be read is left out.
    /// </summary>
    public static IReadOnlyList<string> Save(IDataObject data, IReadOnlyList<Entry> entries, string folder)
    {
        var saved = new List<string>();
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var target = Path.Combine(folder, entry.RelativePath);
            if (entry.IsFolder)
            {
                Directory.CreateDirectory(target);
                saved.Add(entry.RelativePath);
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (WriteContents(data, i, target)) saved.Add(entry.RelativePath);
        }
        return [.. ShelfDrop.TopLevel(saved).Select(p => Path.Combine(folder, p))];
    }

    private static IReadOnlyList<Entry> Parse(nint memory)
    {
        var pointer = GlobalLock(memory);
        if (pointer == 0) return [];
        try
        {
            var size = (long)GlobalSize(memory);
            var count = Marshal.ReadInt32(pointer);
            var entries = new List<Entry>();
            for (var i = 0; i < count && 4 + (long)(i + 1) * DescriptorSize <= size; i++)
            {
                var at = pointer + 4 + i * DescriptorSize;
                var flags = (uint)Marshal.ReadInt32(at);
                var attributes = (uint)Marshal.ReadInt32(at, 36);
                var length = (flags & FdFileSize) != 0 ? ((long)(uint)Marshal.ReadInt32(at, 64) << 32) | (uint)Marshal.ReadInt32(at, 68) : 0;
                var name = Marshal.PtrToStringUni(at + NameOffset) ?? "";
                var folder = (flags & FdAttributes) != 0 && (attributes & FileAttributeDirectory) != 0;
                entries.Add(new Entry(ShelfDrop.SafeRelativePath(name), folder, length));
            }
            return entries;
        }
        finally
        {
            GlobalUnlock(memory);
        }
    }

    private static bool WriteContents(IDataObject data, int index, string target)
    {
        var format = new FORMATETC
        {
            cfFormat = Contents,
            dwAspect = DVASPECT.DVASPECT_CONTENT,
            lindex = index,
            tymed = TYMED.TYMED_ISTREAM | TYMED.TYMED_HGLOBAL,
        };
        STGMEDIUM medium;
        try
        {
            data.GetData(ref format, out medium);
        }
        catch (COMException)
        {
            return false;
        }
        try
        {
            using var file = File.Create(target);
            if (medium.tymed == TYMED.TYMED_ISTREAM) CopyStream((IStream)Marshal.GetObjectForIUnknown(medium.unionmember), file);
            else if (medium.tymed == TYMED.TYMED_HGLOBAL) CopyMemory(medium.unionmember, file);
            else return false;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException)
        {
            return false;
        }
        finally
        {
            ReleaseStgMedium(ref medium);
        }
    }

    private static void CopyStream(IStream source, Stream target)
    {
        var buffer = new byte[Chunk];
        var read = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            while (true)
            {
                source.Read(buffer, buffer.Length, read);
                var count = Marshal.ReadInt32(read);
                if (count <= 0) break;
                target.Write(buffer, 0, count);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(read);
            Marshal.ReleaseComObject(source);
        }
    }

    private static void CopyMemory(nint memory, Stream target)
    {
        var pointer = GlobalLock(memory);
        if (pointer == 0) return;
        try
        {
            var size = (long)GlobalSize(memory);
            var buffer = new byte[Math.Min(Chunk, Math.Max(1, size))];
            for (long done = 0; done < size; done += buffer.Length)
            {
                var count = (int)Math.Min(buffer.Length, size - done);
                Marshal.Copy(pointer + (nint)done, buffer, 0, count);
                target.Write(buffer, 0, count);
            }
        }
        finally
        {
            GlobalUnlock(memory);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterClipboardFormat(string name);

    [DllImport("ole32.dll")]
    private static extern void ReleaseStgMedium(ref STGMEDIUM medium);

    [DllImport("kernel32.dll")]
    private static extern nint GlobalLock(nint memory);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(nint memory);

    [DllImport("kernel32.dll")]
    private static extern nuint GlobalSize(nint memory);
}
