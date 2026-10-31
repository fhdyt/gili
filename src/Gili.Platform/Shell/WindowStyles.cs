using System.Runtime.InteropServices;
using Gili.Platform.Interop;

namespace Gili.Platform.Shell;

public static class WindowStyles
{
    /// <summary>Keeps the bar out of Alt+Tab, and stops clicks on it from taking focus from the active app.</summary>
    public static void MakeNonActivatingToolWindow(IntPtr hwnd)
    {
        var style = Win32.GetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE);
        Win32.SetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE, style | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE);
    }

    /// <summary>
    /// A window the pointer goes through and that never takes focus, like a dragged note: clicks and the wheel reach
    /// whatever is below it, and the window that holds the mouse keeps it.
    /// </summary>
    public static void MakeClickThrough(IntPtr hwnd)
    {
        var style = Win32.GetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE);
        Win32.SetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE, style | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TRANSPARENT);
    }

    /// <summary>Moves a window to a point in screen pixels, keeping its size and place in the z-order, without activating it.</summary>
    public static void MoveTo(IntPtr hwnd, int x, int y) =>
        Win32.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);

    /// <summary>Moves and sizes a window in screen pixels in one step, so its edges never move apart, without activating it.</summary>
    public static void Place(IntPtr hwnd, int x, int y, int width, int height) =>
        Win32.SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height, Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);

    /// <summary>Moves and sizes a window and puts it right below another in the z-order, like a floating note's shadow.</summary>
    public static void PlaceBelow(IntPtr hwnd, IntPtr above, int x, int y, int width, int height) =>
        Win32.SetWindowPos(hwnd, above, x, y, width, height, Win32.SWP_NOACTIVATE);

    /// <summary>Puts a topmost window above every other topmost window, like the island opening over floating notes.</summary>
    public static void BringToTop(IntPtr hwnd) =>
        Win32.SetWindowPos(hwnd, Win32.HWND_TOPMOST, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);

    /// <summary>A window's place and size on screen in pixels.</summary>
    public static (int Left, int Top, int Width, int Height) Bounds(IntPtr hwnd)
    {
        Win32.GetWindowRect(hwnd, out var rect);
        return (rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    /// <summary>The primary monitor's work area in screen pixels: without the AppBar strips and the Windows taskbar.</summary>
    public static (int Left, int Top, int Right, int Bottom) PrimaryWorkArea()
    {
        var info = new Win32.MONITORINFO { cbSize = (uint)Marshal.SizeOf<Win32.MONITORINFO>() };
        Win32.GetMonitorInfo(Win32.MonitorFromPoint(default, Win32.MONITOR_DEFAULTTOPRIMARY), ref info);
        return (info.rcWork.Left, info.rcWork.Top, info.rcWork.Right, info.rcWork.Bottom);
    }

    /// <summary>The island takes focus only while it is open, so Esc and the arrow keys reach it.</summary>
    public static void SetNoActivate(IntPtr hwnd, bool noActivate)
    {
        var style = Win32.GetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE);
        style = noActivate ? style | Win32.WS_EX_NOACTIVATE : style & ~Win32.WS_EX_NOACTIVATE;
        Win32.SetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE, style);
    }

    /// <summary>Windows 11's rounded corners (8 px) for a window that draws its own frame, like Settings.</summary>
    public static void RoundCorners(IntPtr hwnd)
    {
        var round = DWMWCP_ROUND;
        // Fails before Windows 11; the corners stay square.
        _ = DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
    }

    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
