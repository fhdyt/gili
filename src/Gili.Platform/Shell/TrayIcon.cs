using System.Runtime.InteropServices;

namespace Gili.Platform.Shell;

/// <summary>
/// Gili's own icon in the Windows notification area (F-BAR-17, revision 14, issue #25): the mascot's circle with the
/// tooltip "Gili". A click opens Settings and a right-click the bar's menu, so Gili can be quit from where Windows users
/// look for background apps. The bar window receives its messages; <see cref="HandleMessage"/> turns them into events.
/// </summary>
public sealed class TrayIcon(IntPtr hwnd) : IDisposable
{
    /// <summary>The bar's window procedure hands this message to <see cref="HandleMessage"/>.</summary>
    public const int CallbackMessage = 0x8000 + 0x47; // WM_APP + 0x47

    private const uint NIM_ADD = 0, NIM_DELETE = 2, NIM_SETVERSION = 4;
    private const uint NIF_MESSAGE = 0x1, NIF_ICON = 0x2, NIF_TIP = 0x4, NIF_SHOWTIP = 0x80;
    private const uint NOTIFYICON_VERSION_4 = 4;
    private const int WM_CONTEXTMENU = 0x007B, NIN_SELECT = 0x0400, NIN_KEYSELECT = 0x0401;
    private const int SM_CXSMICON = 49;
    private const uint Id = 1;

    private IntPtr _icon;
    private bool _shown;

    /// <summary>A click or Enter on the icon.</summary>
    public event Action? Clicked;

    /// <summary>A right-click or the menu key: the bar's menu opens at the pointer.</summary>
    public event Action? MenuRequested;

    public bool Shown => _shown;

    /// <summary>Adds the icon, or adds it again after Explorer restarted (TaskbarCreated). False when the shell refused.</summary>
    public bool Show()
    {
        if (_shown) Remove();
        if (_icon == IntPtr.Zero) _icon = LoadIcon();
        var data = Data();
        data.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP;
        data.uCallbackMessage = CallbackMessage;
        data.hIcon = _icon;
        data.szTip = "Gili";
        if (!Shell_NotifyIcon(NIM_ADD, ref data)) return false;
        data.uVersion = NOTIFYICON_VERSION_4;
        Shell_NotifyIcon(NIM_SETVERSION, ref data);
        _shown = true;
        return true;
    }

    public void Remove()
    {
        if (!_shown) return;
        var data = Data();
        Shell_NotifyIcon(NIM_DELETE, ref data);
        _shown = false;
    }

    /// <summary>True when the message was the icon's.</summary>
    public bool HandleMessage(int msg, IntPtr lParam)
    {
        if (msg != CallbackMessage) return false;
        // Version 4 puts the event in the low word of lParam.
        switch ((int)(lParam.ToInt64() & 0xFFFF))
        {
            case NIN_SELECT:
            case NIN_KEYSELECT:
                Clicked?.Invoke();
                break;
            case WM_CONTEXTMENU:
                MenuRequested?.Invoke();
                break;
        }
        return true;
    }

    public void Dispose()
    {
        Remove();
        if (_icon != IntPtr.Zero) DestroyIcon(_icon);
        _icon = IntPtr.Zero;
    }

    private NOTIFYICONDATA Data() => new()
    {
        cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = hwnd,
        uID = Id,
        szTip = "",
        szInfo = "",
        szInfoTitle = "",
    };

    /// <summary>Gili.exe's own icon at the small-icon size for this DPI, so the tray never scales a 16 px image up.</summary>
    private static IntPtr LoadIcon()
    {
        var path = Environment.ProcessPath;
        if (path is null) return IntPtr.Zero;
        var size = GetSystemMetrics(SM_CXSMICON);
        return SHDefExtractIcon(path, 0, 0, out var large, out var small, (uint)((size << 16) | size)) == 0
            ? Keep(small, large)
            : IntPtr.Zero;
    }

    private static IntPtr Keep(IntPtr small, IntPtr large)
    {
        if (large != IntPtr.Zero && large != small) DestroyIcon(large);
        return small;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint message, ref NOTIFYICONDATA data);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHDefExtractIcon(string iconFile, int index, uint flags, out IntPtr large, out IntPtr small, uint size);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}
