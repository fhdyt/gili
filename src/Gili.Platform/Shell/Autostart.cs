using Microsoft.Win32;

namespace Gili.Platform.Shell;

/// <summary>The start-with-Windows entry under HKCU\Software\Microsoft\Windows\CurrentVersion\Run (F-BAR-04).</summary>
public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Gili";

    public static void Apply(bool enabled, string exePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(ValueName, Core.LaunchRules.RunCommand(exePath));
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>Prepare to uninstall (PRD 9.4): the entry goes whatever the setting says.</summary>
    public static void Remove()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
