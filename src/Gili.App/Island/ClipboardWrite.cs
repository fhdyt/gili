using System.Collections.Specialized;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Gili.App.Island;

/// <summary>
/// Puts a history image or files back on the clipboard for the Clipboard page's paste (F-ISL-32). Another app may hold
/// the clipboard open, so each write tries a few times.
/// </summary>
public static class ClipboardWrite
{
    /// <summary>The bitmap, and the PNG too for apps that keep transparency.</summary>
    public static bool Image(BitmapSource image)
    {
        var data = new DataObject();
        data.SetImage(image);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        var png = new MemoryStream();
        encoder.Save(png);
        data.SetData("PNG", png);
        return Set(data);
    }

    /// <summary>A file list, marked as a copy so pasting in Explorer never moves the originals.</summary>
    public static bool Files(IReadOnlyList<string> paths)
    {
        var list = new StringCollection();
        list.AddRange([.. paths]);
        var data = new DataObject();
        data.SetFileDropList(list);
        data.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes(1)));
        return Set(data);
    }

    private static bool Set(DataObject data)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(data, copy: true);
                return true;
            }
            catch (COMException)
            {
                Thread.Sleep(20);
            }
        }
        return false;
    }
}
