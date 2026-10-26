using System.Runtime.InteropServices;
using System.Text;
using Gili.Platform.Interop;

namespace Gili.Platform.Shell;

/// <summary>
/// Reserves a strip at the top of the primary monitor so maximized windows start below the bar (F-BAR-01), and
/// keeps it reserved across Explorer restarts, display and DPI changes and resume (F-BAR-03).
/// Unregister must run on every exit path: a strip left registered stays empty until Explorer restarts.
/// </summary>
public sealed class AppBarService(IntPtr hwnd)
{
    private const uint ABM_NEW = 0x0, ABM_REMOVE = 0x1, ABM_QUERYPOS = 0x2, ABM_SETPOS = 0x3;
    private const uint ABM_ACTIVATE = 0x6, ABM_WINDOWPOSCHANGED = 0x9;
    private const uint ABN_POSCHANGED = 0x1, ABN_FULLSCREENAPP = 0x2;
    private const uint ABE_TOP = 1;

    private readonly uint _callbackMessage = Win32.RegisterWindowMessage("Gili.AppBarNotify");
    private readonly uint _taskbarCreated = Win32.RegisterWindowMessage("TaskbarCreated");
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private double _heightDip;
    private bool _registered;
    private bool _positioning;
    private bool _fullscreen;

    /// <summary>True when an app goes fullscreen on the primary monitor, false when it leaves (F-BAR-02).</summary>
    public event Action<bool>? FullscreenChanged;

    public void Register(double heightDip)
    {
        _heightDip = heightDip;
        if (!_registered)
        {
            var data = NewData();
            data.uCallbackMessage = _callbackMessage;
            _registered = Win32.SHAppBarMessage(ABM_NEW, ref data) != UIntPtr.Zero;
        }
        Reposition();
    }

    public void SetHeight(double heightDip)
    {
        _heightDip = heightDip;
        Reposition();
    }

    public void Unregister()
    {
        if (!_registered) return;
        var data = NewData();
        Win32.SHAppBarMessage(ABM_REMOVE, ref data);
        _registered = false;
    }

    /// <summary>Claims the strip again and puts the bar back on top of it, e.g. after a fullscreen app closes.</summary>
    public void Reposition()
    {
        if (!_registered || _positioning) return;
        _positioning = true;
        try
        {
            var monitor = PrimaryMonitor();
            var height = (int)Math.Round(_heightDip * Win32.GetDpiForWindow(hwnd) / 96.0);
            var data = NewData();
            data.uEdge = ABE_TOP;
            data.rc = new Win32.RECT { Left = monitor.Left, Top = monitor.Top, Right = monitor.Right, Bottom = monitor.Top + height };
            Win32.SHAppBarMessage(ABM_QUERYPOS, ref data);
            data.rc.Bottom = data.rc.Top + height;
            Win32.SHAppBarMessage(ABM_SETPOS, ref data);
            Win32.SetWindowPos(hwnd, Win32.HWND_TOPMOST, data.rc.Left, data.rc.Top,
                data.rc.Right - data.rc.Left, height, Win32.SWP_NOACTIVATE);
        }
        finally
        {
            _positioning = false;
        }
    }

    /// <summary>Call from the window's message hook. Returns true for AppBar notifications, which need no further handling.</summary>
    public bool HandleMessage(int msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == _callbackMessage)
        {
            if ((uint)wParam == ABN_POSCHANGED) Reposition();
            else if ((uint)wParam == ABN_FULLSCREENAPP) OnFullscreenApp(lParam != IntPtr.Zero);
            return true;
        }
        if (msg == _taskbarCreated)
        {
            // Explorer restarted and forgot every AppBar.
            _registered = false;
            Register(_heightDip);
            return true;
        }
        switch (msg)
        {
            case WindowMessages.WM_ACTIVATE:
                Notify(ABM_ACTIVATE);
                break;
            case WindowMessages.WM_WINDOWPOSCHANGED:
                Notify(ABM_WINDOWPOSCHANGED);
                break;
            case WindowMessages.WM_DISPLAYCHANGE:
            case WindowMessages.WM_DPICHANGED:
                // Let WPF finish its own DPI resize, then claim the strip at the new size.
                _ui?.Post(_ => Reposition(), null);
                break;
            case WindowMessages.WM_POWERBROADCAST when wParam == WindowMessages.PBT_APMRESUMEAUTOMATIC:
                _ui?.Post(_ => { Unregister(); Register(_heightDip); }, null);
                break;
        }
        return false;
    }

    private void OnFullscreenApp(bool entering)
    {
        // Clicking the desktop can make the shell report its own desktop window as fullscreen.
        if (entering && IsShellDesktop(Win32.GetForegroundWindow())) return;
        if (entering == _fullscreen) return;
        _fullscreen = entering;
        FullscreenChanged?.Invoke(entering);
    }

    private void Notify(uint message)
    {
        if (!_registered) return;
        var data = NewData();
        Win32.SHAppBarMessage(message, ref data);
    }

    private Win32.APPBARDATA NewData() =>
        new() { cbSize = (uint)Marshal.SizeOf<Win32.APPBARDATA>(), hWnd = hwnd };

    private static Win32.RECT PrimaryMonitor()
    {
        var info = new Win32.MONITORINFO { cbSize = (uint)Marshal.SizeOf<Win32.MONITORINFO>() };
        Win32.GetMonitorInfo(Win32.MonitorFromPoint(default, Win32.MONITOR_DEFAULTTOPRIMARY), ref info);
        return info.rcMonitor;
    }

    private static bool IsShellDesktop(IntPtr window)
    {
        var name = new StringBuilder(32);
        Win32.GetClassName(window, name, name.Capacity);
        return name.ToString() is "Progman" or "WorkerW";
    }
}
