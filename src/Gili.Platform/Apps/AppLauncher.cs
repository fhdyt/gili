using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace Gili.Platform.Apps;

/// <summary>
/// Starts a favorite app that isn't running (F-LEFT-08): a Store or pinned app through the shell's Apps folder by its
/// AppUserModelID, a desktop app from its exe in its own folder. Never elevated; Windows asks if the app wants to be.
/// </summary>
public static class AppLauncher
{
    /// <summary>False when the app is gone or Windows refused to start it.</summary>
    public static bool Launch(string appKey)
    {
        try
        {
            if (appKey.StartsWith("aumid:", StringComparison.Ordinal))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", @"shell:AppsFolder\" + appKey[6..]) { UseShellExecute = true })?.Dispose();
                return true;
            }
            if (!appKey.StartsWith("exe:", StringComparison.Ordinal) || !File.Exists(appKey[4..])) return false;
            var path = appKey[4..];
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(path) ?? "" })?.Dispose();
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    /// <summary>A desktop app's exe still exists; Store apps are trusted to be there.</summary>
    public static bool Exists(string appKey) =>
        !appKey.StartsWith("exe:", StringComparison.Ordinal) || File.Exists(appKey[4..]);

    /// <summary>
    /// The app key for a file picked in Settings: an exe as itself, a shortcut by the exe it points to; null for anything
    /// else (a shortcut to a document or a folder).
    /// </summary>
    public static string? KeyForFile(string path)
    {
        if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return File.Exists(path) ? "exe:" + path : null;
        if (!path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) return null;
        var target = Shortcuts.Target(path);
        return target is not null && target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(target) ? "exe:" + target : null;
    }
}
