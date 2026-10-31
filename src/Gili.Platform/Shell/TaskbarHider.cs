using System.Runtime.InteropServices;
using System.Text;
using Gili.Core.Taskbar;
using Gili.Platform.Interop;
using static Gili.Platform.Interop.ShellInterop;

namespace Gili.Platform.Shell;

/// <summary>
/// The primary monitor's Windows taskbar for Hide Windows taskbar (F-BAR-11, design 3f): auto-hide through
/// <c>ABM_SETSTATE</c>, the window itself through <c>ShowWindow</c>, and an out-of-context WinEvent on Explorer that hides
/// it again whenever Explorer shows it. Create it on the UI thread, whose message loop delivers the events.
/// </summary>
public sealed class TaskbarHider : ITaskbar, IDisposable
{
    private const uint ABM_GETSTATE = 0x4, ABM_SETSTATE = 0xA;
    private const int ABS_AUTOHIDE = 0x1, ABS_ALWAYSONTOP = 0x2;
    private const uint EVENT_OBJECT_SHOW = 0x8002;
    private const int SW_HIDE = 0, SW_SHOWNA = 8;
    private static readonly TimeSpan RevealCheck = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan RevealGrace = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RevealLimit = TimeSpan.FromSeconds(60);
    /// <summary>Explorer windows that are not the taskbar or its flyouts.</summary>
    private static readonly string[] NotTaskbar = ["CabinetWClass", "Progman", "WorkerW"];

    private readonly WinEventProc _proc;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private nint _hook;
    private uint _hookedProcess;
    private nint _tray;
    private bool _hidden;
    private bool _revealing;
    private Timer? _revealTimer;
    private DateTime _revealedAt;

    public TaskbarHider() => _proc = OnEvent;

    public bool AutoHide => ((int)State() & ABS_AUTOHIDE) != 0;

    public void SetAutoHide(bool on)
    {
        var data = NewData();
        data.lParam = on ? ABS_AUTOHIDE : ABS_ALWAYSONTOP;
        Win32.SHAppBarMessage(ABM_SETSTATE, ref data);
    }

    public void Hide()
    {
        _hidden = true;
        EndReveal();
        Find();
        Hook();
        if (_tray != 0) ShowWindow(_tray, SW_HIDE);
    }

    public void Show()
    {
        _hidden = false;
        EndReveal();
        Unhook();
        Find();
        if (_tray != 0) ShowWindow(_tray, SW_SHOWNA);
    }

    /// <summary>
    /// Shows the hidden taskbar for the Tray button, which opens Windows' hidden-icons flyout from it, and hides it again
    /// once the taskbar and its flyouts lose the focus (design 3f). False when the taskbar is not hidden.
    /// </summary>
    public bool Reveal()
    {
        if (!_hidden || _tray == 0) return false;
        _revealing = true;
        _revealedAt = DateTime.UtcNow;
        ShowWindow(_tray, SW_SHOWNA);
        _revealTimer ??= new Timer(_ => Post(CheckReveal));
        _revealTimer.Change(RevealCheck, RevealCheck);
        return true;
    }

    public void Dispose()
    {
        _revealTimer?.Dispose();
        _revealTimer = null;
        Unhook();
    }

    private void CheckReveal()
    {
        if (!_revealing) return;
        var shown = DateTime.UtcNow - _revealedAt;
        if (shown < RevealGrace) return;
        if (shown < RevealLimit && OnTaskbar(Win32.GetForegroundWindow())) return;
        EndReveal();
        if (_hidden && _tray != 0) ShowWindow(_tray, SW_HIDE);
    }

    private void EndReveal()
    {
        _revealing = false;
        _revealTimer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private bool OnTaskbar(nint window)
    {
        if (window == 0) return false;
        GetWindowThreadProcessId(window, out var process);
        if (process != _hookedProcess) return false;
        var name = new StringBuilder(64);
        Win32.GetClassName(window, name, name.Capacity);
        return !NotTaskbar.Contains(name.ToString());
    }

    private void OnEvent(nint hook, uint eventType, nint hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (!_hidden || _revealing || idObject != OBJID_WINDOW || hwnd != _tray) return;
        ShowWindow(_tray, SW_HIDE);
    }

    /// <summary>The taskbar window and Explorer's process; both change when Explorer restarts.</summary>
    private void Find()
    {
        _tray = FindWindowEx(0, 0, "Shell_TrayWnd", null);
    }

    private void Hook()
    {
        if (_tray == 0) return;
        GetWindowThreadProcessId(_tray, out var process);
        if (_hook != 0 && process == _hookedProcess) return;
        Unhook();
        _hook = SetWinEventHook(EVENT_OBJECT_SHOW, EVENT_OBJECT_SHOW, 0, _proc, process, 0, WINEVENT_OUTOFCONTEXT);
        _hookedProcess = process;
    }

    private void Unhook()
    {
        if (_hook == 0) return;
        UnhookWinEvent(_hook);
        _hook = 0;
    }

    private void Post(Action action)
    {
        if (_ui is null) action();
        else _ui.Post(_ => action(), null);
    }

    private static nuint State()
    {
        var data = NewData();
        return Win32.SHAppBarMessage(ABM_GETSTATE, ref data);
    }

    private static Win32.APPBARDATA NewData() => new() { cbSize = (uint)Marshal.SizeOf<Win32.APPBARDATA>() };
}
