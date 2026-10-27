using System.Runtime.InteropServices;

namespace Gili.Platform.Clipboard;

/// <summary>
/// Hears every clipboard change through AddClipboardFormatListener on a message-only window: no polling (PRD 8.1).
/// Create it on the UI thread; Changed is raised there, from that thread's message loop.
/// </summary>
public sealed class ClipboardMonitor : IDisposable
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;
    private static readonly IntPtr HWND_MESSAGE = new(-3);

    private readonly WndProc _proc;
    private readonly IntPtr _hwnd;

    public ClipboardMonitor()
    {
        _proc = OnMessage;
        var className = "GiliClipboard" + Environment.ProcessId;
        var wc = new WNDCLASS { lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_proc), lpszClassName = className };
        RegisterClass(ref wc);
        _hwnd = CreateWindowEx(0, className, "", 0, 0, 0, 0, 0, HWND_MESSAGE, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (_hwnd != IntPtr.Zero) AddClipboardFormatListener(_hwnd);
    }

    public event Action? Changed;

    public void Dispose()
    {
        if (_hwnd == IntPtr.Zero) return;
        RemoveClipboardFormatListener(_hwnd);
        DestroyWindow(_hwnd);
    }

    private IntPtr OnMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_CLIPBOARDUPDATE) Changed?.Invoke();
        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private delegate IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASS
    {
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClass(ref WNDCLASS wc);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(int exStyle, string className, string name, int style, int x, int y, int w, int h,
        IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr DefWindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
}
