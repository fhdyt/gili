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
