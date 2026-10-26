using Gili.Platform.Interop;

namespace Gili.Platform.Shell;

/// <summary>Remembers which window had focus before the island opened, and gives it back when the island closes.</summary>
public static class ForegroundWindow
{
    public static IntPtr Get() => Win32.GetForegroundWindow();

    public static void Restore(IntPtr hwnd)
    {
        if (hwnd != IntPtr.Zero) Win32.SetForegroundWindow(hwnd);
    }
}
