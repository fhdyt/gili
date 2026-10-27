using System.Runtime.InteropServices;

namespace Gili.Platform.Power;

/// <summary>
/// Whether the screen is on, from Windows' display-state notification on the bar's window (PRD 8.1: polling pauses
/// while the screen is off). The window receives WM_POWERBROADCAST / PBT_POWERSETTINGCHANGE; pass it to TryRead.
/// </summary>
public sealed class DisplayMonitor : IDisposable
{
    public const nint PBT_POWERSETTINGCHANGE = 0x8013;
    private const int DEVICE_NOTIFY_WINDOW_HANDLE = 0;
    private static readonly Guid GUID_CONSOLE_DISPLAY_STATE = new("6FE69556-704A-47A0-8F24-C28D936FDA47");

    private readonly IntPtr _registration;

    public DisplayMonitor(IntPtr hwnd)
    {
        var guid = GUID_CONSOLE_DISPLAY_STATE;
        _registration = RegisterPowerSettingNotification(hwnd, ref guid, DEVICE_NOTIFY_WINDOW_HANDLE);
    }

    /// <summary>Reads a POWERBROADCAST_SETTING: 0 = off, 1 = on, 2 = dimmed (still on).</summary>
    public static bool TryRead(IntPtr lParam, out bool screenOn)
    {
        screenOn = true;
        if (lParam == IntPtr.Zero || Marshal.PtrToStructure<Guid>(lParam) != GUID_CONSOLE_DISPLAY_STATE) return false;
        screenOn = Marshal.ReadInt32(lParam, 20) != 0;
        return true;
    }

    public void Dispose()
    {
        if (_registration != IntPtr.Zero) UnregisterPowerSettingNotification(_registration);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr RegisterPowerSettingNotification(IntPtr recipient, ref Guid setting, int flags);

    [DllImport("user32.dll")]
    private static extern bool UnregisterPowerSettingNotification(IntPtr handle);
}
