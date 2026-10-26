using System.Diagnostics;
using Gili.Core.Media;

namespace Gili.Platform.Media;

public static class VlcWindow
{
    /// <summary>The video title from a running VLC window, or null when VLC is closed or idle.</summary>
    public static string? ReadTitle()
    {
        var processes = Process.GetProcessesByName("vlc");
        try
        {
            return processes.Select(p => MediaRules.VlcTitle(p.MainWindowTitle)).FirstOrDefault(t => t is not null);
        }
        finally
        {
            foreach (var process in processes) process.Dispose();
        }
    }
}
