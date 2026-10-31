using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Gili.Platform.Power;

/// <summary>
/// Whether Windows has updates waiting for a restart, which is when Start offers "Update and shut down" and "Update and
/// restart" (PRD 9.2, F-RIGHT-08): the Windows Update Agent's <c>ISystemInformation.RebootRequired</c>, or its
/// <c>RebootRequired</c> registry key. Both read true on the user's laptop with an update staged (plan 3e).
/// </summary>
public static class PendingUpdates
{
    private const string RebootKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired";

    /// <summary>Takes up to tens of milliseconds (COM), so it is called off the UI thread.</summary>
    public static bool Read()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(RebootKey);
            if (key is not null) return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            // Fall through to the agent.
        }
        return AgentSaysRebootRequired();
    }

    private static bool AgentSaysRebootRequired()
    {
        object? info = null;
        try
        {
            if (Type.GetTypeFromProgID("Microsoft.Update.SystemInfo") is not { } type) return false;
            info = Activator.CreateInstance(type);
            return info is not null && type.InvokeMember("RebootRequired", System.Reflection.BindingFlags.GetProperty, null, info, null) is true;
        }
        catch (Exception ex) when (ex is COMException or System.Reflection.TargetInvocationException or UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            if (info is not null && Marshal.IsComObject(info)) Marshal.ReleaseComObject(info);
        }
    }

    private const uint SHUTDOWN_RESTART = 0x00000004;
    private const uint SHUTDOWN_POWEROFF = 0x00000008;
    private const uint SHUTDOWN_INSTALL_UPDATES = 0x00000040;
    // Operating system: upgrade (planned), as Start reports it.
    private const uint Reason = 0x80000000 | 0x00020000 | 0x00000003;

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint InitiateShutdownW(string? machine, string? message, uint grace, uint flags, uint reason);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool LookupPrivilegeValueW(string? system, string name, out long luid);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll, ref TokenPrivilege state, uint length, IntPtr previous, IntPtr returned);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct TokenPrivilege
    {
        public uint Count;
        public long Luid;
        public uint Attributes;
    }

    /// <summary>
    /// Installs the waiting updates, then shuts down; apps with unsaved work still ask first (PRD 6.4). False when
    /// Windows refused, so the caller can open Windows Update instead (PRD 12).
    /// </summary>
    public static bool UpdateAndShutDown() => Shutdown(SHUTDOWN_POWEROFF | SHUTDOWN_INSTALL_UPDATES);

    /// <summary>Installs the waiting updates, then restarts; false when Windows refused.</summary>
    public static bool UpdateAndRestart() => Shutdown(SHUTDOWN_RESTART | SHUTDOWN_INSTALL_UPDATES);

    /// <summary>
    /// <c>InitiateShutdownW</c> needs the shutdown privilege, which every user holds but which starts disabled, the same
    /// way <c>shutdown.exe</c> turns it on. Nothing is forced: no <c>SHUTDOWN_FORCE_*</c>, no grace period.
    /// </summary>
    private static bool Shutdown(uint flags)
    {
        try
        {
            EnableShutdownPrivilege();
        }
        catch (Win32Exception)
        {
            return false;
        }
        return InitiateShutdownW(null, null, 0, flags, Reason) == 0;
    }

    private static void EnableShutdownPrivilege()
    {
        const uint TOKEN_ADJUST_PRIVILEGES = 0x20;
        const uint TOKEN_QUERY = 0x8;
        const uint SE_PRIVILEGE_ENABLED = 0x2;
        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out var token)) throw new Win32Exception();
        try
        {
            if (!LookupPrivilegeValueW(null, "SeShutdownPrivilege", out var luid)) throw new Win32Exception();
            var state = new TokenPrivilege { Count = 1, Luid = luid, Attributes = SE_PRIVILEGE_ENABLED };
            if (!AdjustTokenPrivileges(token, false, ref state, 0, IntPtr.Zero, IntPtr.Zero)) throw new Win32Exception();
        }
        finally
        {
            CloseHandle(token);
        }
    }
}
