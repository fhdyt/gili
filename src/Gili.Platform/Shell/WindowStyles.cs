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

    /// <summary>The island takes focus only while it is open, so Esc and the arrow keys reach it.</summary>
    public static void SetNoActivate(IntPtr hwnd, bool noActivate)
    {
        var style = Win32.GetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE);
        style = noActivate ? style | Win32.WS_EX_NOACTIVATE : style & ~Win32.WS_EX_NOACTIVATE;
        Win32.SetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE, style);
    }
}
