using System.Runtime.InteropServices;
using Gili.Platform.Apps;
using Gili.Platform.Interop;
using static Gili.Platform.Interop.ShellInterop;

namespace Gili.Platform.Clipboard;

/// <summary>
/// The Clipboard page's paste (F-ISL-28, design 4a): the text goes onto the clipboard, then Ctrl+V into the window that
/// was in front, unless that window runs as admin (Windows blocks input from a normal process to it).
/// </summary>
public static class ClipboardPaste
{
    private const uint CfUnicodeText = 13;
    private const uint GmemMoveable = 0x0002;
    private const ushort VkControl = 0x11;
    private const ushort VkV = 0x56;
    private const uint TokenElevation = 20;
    private const uint TokenQuery = 0x0008;

    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Puts plain text on the clipboard; another app may hold it open, so it tries a few times.</summary>
    public static bool SetText(string text)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (TrySetText(text)) return true;
            Thread.Sleep(20);
        }
        return false;
    }

    /// <summary>Whether Ctrl+V can reach the window: it exists, isn't the desktop, and doesn't run as admin.</summary>
    public static bool CanPasteInto(nint hwnd)
    {
        if (hwnd == 0 || !IsWindow(hwnd)) return false;
        var name = new System.Text.StringBuilder(64);
        Win32.GetClassName(hwnd, name, name.Capacity);
        if (name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd") return false;
        return !IsElevated(hwnd);
    }

    public static nint Foreground() => Win32.GetForegroundWindow();

    /// <summary>Ctrl+V, the way the keyboard sends it.</summary>
    public static void SendCtrlV()
    {
        INPUT[] keys =
        [
            new() { type = INPUT_KEYBOARD, ki = new KEYBDINPUT { wVk = VkControl } },
            new() { type = INPUT_KEYBOARD, ki = new KEYBDINPUT { wVk = VkV } },
            new() { type = INPUT_KEYBOARD, ki = new KEYBDINPUT { wVk = VkV, dwFlags = KEYEVENTF_KEYUP } },
            new() { type = INPUT_KEYBOARD, ki = new KEYBDINPUT { wVk = VkControl, dwFlags = KEYEVENTF_KEYUP } },
        ];
        SendInput((uint)keys.Length, keys, Marshal.SizeOf<INPUT>());
    }

    /// <summary>The copying app's name as the bar shows it (Chrome, Terminal), cached per program; null when unknown.</summary>
    public static string? SourceApp(nint ownerWindow)
    {
        if (ownerWindow == 0) return null;
        var key = TaskbarWindows.KeyOf(ownerWindow);
        if (key.StartsWith("hwnd:", StringComparison.Ordinal)) return null;
        if (Names.TryGetValue(key, out var known)) return known;
        var lookup = key.StartsWith("aumid:", StringComparison.Ordinal) || TaskbarWindows.PackagedAppId(ownerWindow) is not { } id ? key : "aumid:" + id;
        return Names[key] = AppInfo.Name(lookup, ownerWindow);
    }

    private static bool TrySetText(string text)
    {
        if (!OpenClipboard(0)) return false;
        try
        {
            if (!EmptyClipboard()) return false;
            var bytes = (text.Length + 1) * 2;
            var memory = GlobalAlloc(GmemMoveable, (nuint)bytes);
            if (memory == 0) return false;
            var target = GlobalLock(memory);
            if (target == 0) return false;
            try
            {
                Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
                Marshal.WriteInt16(target, text.Length * 2, 0);
            }
            finally
            {
                GlobalUnlock(memory);
            }
            // The clipboard owns the memory once this succeeds.
            return SetClipboardData(CfUnicodeText, memory) != 0;
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static bool IsElevated(nint hwnd)
    {
        GetWindowThreadProcessId(hwnd, out var pid);
        var process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        // A process Gili may not even query is one it may not type into either.
        if (process == 0) return true;
        try
        {
            if (!OpenProcessToken(process, TokenQuery, out var token)) return true;
            try
            {
                return GetTokenInformation(token, TokenElevation, out var elevated, sizeof(int), out _) && elevated != 0;
            }
            finally
            {
                CloseHandle(token);
            }
        }
        finally
        {
            CloseHandle(process);
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(nint owner);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll")]
    private static extern nint SetClipboardData(uint format, nint memory);

    [DllImport("kernel32.dll")]
    private static extern nint GlobalAlloc(uint flags, nuint bytes);

    [DllImport("kernel32.dll")]
    private static extern nint GlobalLock(nint memory);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(nint memory);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(nint process, uint access, out nint token);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(nint token, uint informationClass, out int information, int length, out int returned);
}
