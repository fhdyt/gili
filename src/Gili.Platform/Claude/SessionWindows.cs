using System.Runtime.InteropServices;
using Gili.Core.Claude;
using Gili.Platform.Apps;
using Gili.Platform.Interop;
using static Gili.Platform.Interop.ShellInterop;

namespace Gili.Platform.Claude;

/// <summary>A session's window, the process that owns it, and that program's name (Windows Terminal, Claude).</summary>
public sealed record SessionWindow(nint Handle, int Pid, string Program);

/// <summary>
/// Finds the window a Claude Code session runs in (F-CLD-06, design 3i): one process snapshot for the parents and one
/// pass over the top-level windows, then <see cref="ClaudeWindows.Find"/>. Called only on Stop, a click, a new PID, or a
/// foreground change while a finish is unseen (PRD 8.1).
/// </summary>
public static class SessionWindows
{
    private const uint SnapProcesses = 0x2;

    public static SessionWindow? Find(int pid)
    {
        if (pid <= 0) return null;
        var parents = Parents();
        var windows = Windows();
        var starts = new Dictionary<int, long>();
        ProcessFacts? Facts(int id)
        {
            if (!parents.TryGetValue(id, out var p)) return null;
            if (!starts.TryGetValue(id, out var start)) starts[id] = start = StartTicks(id);
            return new ProcessFacts(p.Parent, start, p.Name);
        }
        var found = ClaudeWindows.Find(pid, Facts, id => windows.GetValueOrDefault(id));
        return found is { } f ? new SessionWindow(f.Window, f.Pid, Program(f.Window)) : null;
    }

    /// <summary>The process that owns the foreground window; 0 when none.</summary>
    public static int ForegroundPid()
    {
        var hwnd = Win32.GetForegroundWindow();
        if (hwnd == 0) return 0;
        GetWindowThreadProcessId(hwnd, out var pid);
        return (int)pid;
    }

    public static int PidOf(nint hwnd)
    {
        if (hwnd == 0) return 0;
        GetWindowThreadProcessId(hwnd, out var pid);
        return (int)pid;
    }

    /// <summary>Restores the window when minimized and brings it to the front.</summary>
    public static void BringToFront(SessionWindow window) => TaskbarWindows.BringToFront(window.Handle);

    /// <summary>Every process's parent and file name, from one Toolhelp snapshot.</summary>
    private static Dictionary<int, (int Parent, string Name)> Parents()
    {
        var map = new Dictionary<int, (int, string)>();
        var snapshot = CreateToolhelp32Snapshot(SnapProcesses, 0);
        if (snapshot == -1) return map;
        try
        {
            var entry = new PROCESSENTRY32W { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32W>() };
            for (var ok = Process32FirstW(snapshot, ref entry); ok; ok = Process32NextW(snapshot, ref entry))
                map[(int)entry.th32ProcessID] = ((int)entry.th32ParentProcessID, entry.szExeFile);
        }
        finally
        {
            CloseHandle(snapshot);
        }
        return map;
    }

    /// <summary>
    /// Each process's first top-level window in z-order that the taskbar would show: visible, not a tool window, without
    /// an owner. A cloaked one (another virtual desktop) counts only when its process has no other.
    /// </summary>
    private static Dictionary<int, nint> Windows()
    {
        var shown = new Dictionary<int, nint>();
        var cloaked = new Dictionary<int, nint>();
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd) || GetWindow(hwnd, GW_OWNER) != 0) return true;
            var style = Win32.GetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE);
            if ((style & Win32.WS_EX_TOOLWINDOW) != 0) return true;
            GetWindowThreadProcessId(hwnd, out var pid);
            var hidden = DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out var cloak, sizeof(int)) == 0 && cloak != 0;
            (hidden ? cloaked : shown).TryAdd((int)pid, hwnd);
            return true;
        }, 0);
        foreach (var (pid, hwnd) in cloaked) shown.TryAdd(pid, hwnd);
        return shown;
    }

    /// <summary>When the process started, in UTC ticks; 0 when it can't be read (another user's or an elevated one).</summary>
    private static long StartTicks(int pid)
    {
        var process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
        if (process == 0) return 0;
        try
        {
            return GetProcessTimes(process, out var created, out _, out _, out _) ? created : 0;
        }
        finally
        {
            CloseHandle(process);
        }
    }

    /// <summary>
    /// The app's name as the Start menu shows it (Terminal, Claude): a packaged program by its package, not by its exe's
    /// description (Windows Terminal Host).
    /// </summary>
    private static string Program(nint hwnd)
    {
        var key = TaskbarWindows.KeyOf(hwnd);
        if (!key.StartsWith("aumid:", StringComparison.Ordinal) && TaskbarWindows.PackagedAppId(hwnd) is { } id) key = "aumid:" + id;
        return AppInfo.Name(key, hwnd);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PROCESSENTRY32W
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public nint th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32FirstW(nint snapshot, ref PROCESSENTRY32W entry);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32NextW(nint snapshot, ref PROCESSENTRY32W entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(nint process, out long creation, out long exit, out long kernel, out long user);
}
