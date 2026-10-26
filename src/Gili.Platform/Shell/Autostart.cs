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
        if (enabled) key.SetValue(ValueName, $"\"{exePath}\"");
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
