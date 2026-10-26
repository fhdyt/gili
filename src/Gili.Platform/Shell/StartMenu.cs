using System.Runtime.InteropServices;
using static Gili.Platform.Interop.ShellInterop;

namespace Gili.Platform.Shell;

/// <summary>Opens or closes the Windows Start menu, the same as pressing the Windows key (PRD 9.2 "Start").</summary>
public static class StartMenu
{
    public static void Toggle()
    {
        INPUT[] keys =
        [
            new() { type = INPUT_KEYBOARD, ki = new KEYBDINPUT { wVk = VK_LWIN } },
            new() { type = INPUT_KEYBOARD, ki = new KEYBDINPUT { wVk = VK_LWIN, dwFlags = KEYEVENTF_KEYUP } },
        ];
        SendInput((uint)keys.Length, keys, Marshal.SizeOf<INPUT>());
    }
}
