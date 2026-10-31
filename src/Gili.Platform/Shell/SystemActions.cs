using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static Gili.Platform.Interop.ShellInterop;

namespace Gili.Platform.Shell;

/// <summary>Windows panels and power actions the bar hands off to (PRD 6.4).</summary>
public static class SystemActions
{
    private const ushort VK_A = 0x41;
    private const ushort VK_B = 0x42;
    private const ushort VK_RETURN = 0x0D;

    /// <summary>Windows Quick Settings, the same as Win+A.</summary>
    public static void QuickSettings() => Press([VK_LWIN, VK_A]);

    /// <summary>The Windows hidden-icons flyout: Win+B moves to the notification area, Enter opens its chevron.</summary>
    public static void HiddenIcons()
    {
        Press([VK_LWIN, VK_B]);
        Press([VK_RETURN]);
    }

    public static void Sleep() => SetSuspendState(false, false, false);

    public static void ShutDown() => Run("shutdown.exe", "/s /t 0");

    public static void Restart() => Run("shutdown.exe", "/r /t 0");

    /// <summary>Opens an ms-settings: page.</summary>
    public static void OpenSettings(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch (Win32Exception)
        {
            // Settings is missing or blocked by policy; nothing else to open.
        }
    }

    /// <summary>A folder in Explorer, e.g. the logs from Settings → About.</summary>
    public static void OpenFolder(string path) => OpenSettings(path);

    /// <summary>Task Manager, from the System page (PRD 6.3); it asks for elevation itself when its policy wants it.</summary>
    public static void TaskManager() => OpenSettings("taskmgr.exe");

    private static void Run(string file, string arguments)
    {
        try
        {
            Process.Start(new ProcessStartInfo(file, arguments) { UseShellExecute = false, CreateNoWindow = true });
        }
        catch (Win32Exception)
        {
            // Blocked by policy; Windows shows its own message for shutdown rights.
        }
    }

    /// <summary>Presses the keys together and lets them go in reverse order.</summary>
    private static void Press(ushort[] keys)
    {
        var inputs = keys.Select(k => new INPUT { type = INPUT_KEYBOARD, ki = new KEYBDINPUT { wVk = k } })
            .Concat(keys.Reverse().Select(k => new INPUT { type = INPUT_KEYBOARD, ki = new KEYBDINPUT { wVk = k, dwFlags = KEYEVENTF_KEYUP } }))
            .ToArray();
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    [DllImport("powrprof.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool SetSuspendState(
        [MarshalAs(UnmanagedType.U1)] bool hibernate, [MarshalAs(UnmanagedType.U1)] bool force, [MarshalAs(UnmanagedType.U1)] bool wakeupEventsDisabled);
}
