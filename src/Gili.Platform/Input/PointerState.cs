using System.Runtime.InteropServices;
using Gili.Platform.Interop;

namespace Gili.Platform.Input;

/// <summary>The pointer position in physical pixels and whether the left and right buttons are down.</summary>
public readonly record struct PointerSample(int X, int Y, bool Left, bool Right);

/// <summary>
/// Reads the pointer, the mouse buttons and the time since the last input on demand, so Gili follows the mouse
/// without a global hook (PRD 6.3, 8.1).
/// </summary>
public static class PointerState
{
    public static PointerSample Read()
    {
        Win32.GetCursorPos(out var point);
        // GetAsyncKeyState reads the physical buttons; with swapped buttons the physical right one is the primary.
        var swapped = Win32.GetSystemMetrics(Win32.SM_SWAPBUTTON) != 0;
        var physicalLeft = Win32.GetAsyncKeyState(Win32.VK_LBUTTON) < 0;
        var physicalRight = Win32.GetAsyncKeyState(Win32.VK_RBUTTON) < 0;
        return new(point.X, point.Y, swapped ? physicalRight : physicalLeft, swapped ? physicalLeft : physicalRight);
    }

    /// <summary>Time since the last keyboard or mouse input anywhere in the session.</summary>
    public static TimeSpan IdleTime()
    {
        var info = new Win32.LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<Win32.LASTINPUTINFO>() };
        if (!Win32.GetLastInputInfo(ref info)) return TimeSpan.Zero;
        // dwTime is a 32-bit tick count that wraps every 49.7 days; unsigned subtraction handles the wrap.
        return TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.dwTime));
    }
}
