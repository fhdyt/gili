using System.Runtime.InteropServices;
using Gili.Core.Apps;
using Gili.Platform.Interop;
using static Gili.Platform.Interop.ShellInterop;

namespace Gili.Platform.Apps;

/// <summary>
/// The windows the Windows taskbar shows and the foreground window (PRD 9.2 "Aplikasi berjalan dan aktif"), from
/// <c>EnumWindows</c> and out-of-context WinEvents: no global hook, no admin. Create it on the UI thread, whose message
/// loop delivers the events.
/// </summary>
public sealed class TaskbarWindows : IDisposable
{
    private static readonly string[] Ignored = ["Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd"];

    private readonly WinEventProc _proc;
    private readonly List<nint> _hooks = [];
    private readonly uint _ownProcess = (uint)Environment.ProcessId;
    private readonly Dictionary<nint, string> _keys = [];

    public TaskbarWindows()
    {
        _proc = OnEvent;
        // Destroy, show and hide are 0x8001–0x8003.
        Hook(EVENT_OBJECT_DESTROY, EVENT_OBJECT_HIDE);
        Hook(EVENT_OBJECT_CLOAKED, EVENT_OBJECT_UNCLOAKED);
        Hook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND);
    }

    /// <summary>A top-level window appeared, disappeared or was cloaked; read the list again.</summary>
    public event Action? Changed;

    public event Action<nint>? ForegroundChanged;

    /// <summary>All top-level windows, top of the z-order first, with the app each belongs to.</summary>
    public IReadOnlyList<TaskbarWindow> Read()
    {
        var windows = new List<TaskbarWindow>();
        EnumWindows((hwnd, _) =>
        {
            var window = Describe(hwnd);
            if (window is not null) windows.Add(window);
            return true;
        }, 0);
        // Forget the keys of windows that are gone, so a reused handle never keeps another app's key.
        var alive = windows.Select(w => w.Handle).ToHashSet();
        foreach (var gone in _keys.Keys.Where(h => !alive.Contains(h)).ToList()) _keys.Remove(gone);
        return windows;
    }

    /// <summary>The app a window belongs to: its AppUserModelID when it has one, else its executable's path.</summary>
    public string AppKeyOf(nint hwnd)
    {
        if (_keys.TryGetValue(hwnd, out var key)) return key;
        key = ResolveKey(hwnd);
        _keys[hwnd] = key;
        return key;
    }

    /// <summary>Restores the window when minimized and brings it to the front, as a taskbar button does.</summary>
    public static void BringToFront(nint hwnd)
    {
        if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
        Win32.SetForegroundWindow(hwnd);
    }

    public void Dispose()
    {
        foreach (var hook in _hooks) UnhookWinEvent(hook);
        _hooks.Clear();
    }

    private void Hook(uint min, uint max)
    {
        var hook = SetWinEventHook(min, max, 0, _proc, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        if (hook != 0) _hooks.Add(hook);
    }

    private void OnEvent(nint hook, uint eventType, nint hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (hwnd == 0 || idObject != OBJID_WINDOW || idChild != 0) return;
        if (eventType == EVENT_SYSTEM_FOREGROUND)
        {
            ForegroundChanged?.Invoke(hwnd);
            return;
        }
        // A destroyed window can no longer report its ancestor; any top-level change is worth one more read.
        if (eventType == EVENT_OBJECT_DESTROY || GetAncestor(hwnd, GA_ROOT) == hwnd) Changed?.Invoke();
    }

    private TaskbarWindow? Describe(nint hwnd)
    {
        var className = ClassName(hwnd);
        if (Ignored.Contains(className)) return null;
        var visible = IsWindowVisible(hwnd);
        if (!visible) return null;
        var style = Win32.GetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE);
        var appWindow = (style & WS_EX_APPWINDOW) != 0;
        // Like the taskbar: tool windows and windows that never activate get no button unless they ask for one.
        var tool = (style & Win32.WS_EX_TOOLWINDOW) != 0 || ((style & Win32.WS_EX_NOACTIVATE) != 0 && !appWindow);
        var owned = GetWindow(hwnd, GW_OWNER) != 0;
        var cloaked = DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out var cloak, sizeof(int)) == 0 && cloak != 0;
        GetWindowThreadProcessId(hwnd, out var process);
        var window = new TaskbarWindow(hwnd, "", visible, tool, owned, appWindow, cloaked, process == _ownProcess);
        return TaskbarFilter.Shows(window) ? window with { AppKey = AppKeyOf(hwnd) } : null;
    }

    private static string ResolveKey(nint hwnd)
    {
        if (WindowAppId(hwnd) is { } id) return "aumid:" + id;
        // Store apps live inside ApplicationFrameHost; their own process has the AppUserModelID.
        if (ClassName(hwnd) == "ApplicationFrameWindow")
        {
            var core = FindWindowEx(hwnd, 0, "Windows.UI.Core.CoreWindow", null);
            if (core != 0 && ProcessAppId(core) is { } packaged) return "aumid:" + packaged;
        }
        return ProcessPath(hwnd) is { } path ? "exe:" + path.ToLowerInvariant() : "hwnd:" + hwnd;
    }

    /// <summary>The window's own AppUserModelID (set by most apps that pin), from its property store.</summary>
    private static string? WindowAppId(nint hwnd)
    {
        var iid = typeof(IPropertyStore).GUID;
        if (SHGetPropertyStoreForWindow(hwnd, ref iid, out var store) != 0 || store is null) return null;
        try
        {
            var key = AppUserModelId;
            if (store.GetValue(ref key, out var value) != 0) return null;
            try
            {
                return value.vt == VT_LPWSTR && value.pointer != 0 ? Marshal.PtrToStringUni(value.pointer) : null;
            }
            finally
            {
                PropVariantClear(ref value);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(store);
        }
    }

    private static string? ProcessAppId(nint hwnd)
    {
        GetWindowThreadProcessId(hwnd, out var pid);
        var process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == 0) return null;
        try
        {
            uint length = 256;
            var buffer = new char[length];
            return GetApplicationUserModelId(process, ref length, buffer) == 0 ? new string(buffer, 0, (int)length - 1) : null;
        }
        finally
        {
            CloseHandle(process);
        }
    }

    public static string? ProcessPath(nint hwnd)
    {
        GetWindowThreadProcessId(hwnd, out var pid);
        var process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == 0) return null;
        try
        {
            uint size = 1024;
            var buffer = new char[size];
            return QueryFullProcessImageName(process, 0, buffer, ref size) ? new string(buffer, 0, (int)size) : null;
        }
        finally
        {
            CloseHandle(process);
        }
    }

    private static string ClassName(nint hwnd)
    {
        var name = new System.Text.StringBuilder(64);
        Win32.GetClassName(hwnd, name, name.Capacity);
        return name.ToString();
    }
}
