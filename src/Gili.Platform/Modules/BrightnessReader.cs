using System.Management;

namespace Gili.Platform.Modules;

/// <summary>The built-in display's brightness from WMI; null when the display has no brightness control (PRD 6.4).</summary>
public static class BrightnessReader
{
    public static int? Read()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT CurrentBrightness FROM WmiMonitorBrightness");
            foreach (var item in searcher.Get())
            {
                using (item) return Convert.ToInt32(item["CurrentBrightness"], System.Globalization.CultureInfo.InvariantCulture);
            }
            return null;
        }
        catch (ManagementException)
        {
            return null;
        }
    }
}
