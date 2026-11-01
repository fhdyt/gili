using Gili.Core.Apps;

namespace Gili.Core.Claude;

/// <summary>Claude's sessions and 5-hour timer show only while Claude Desktop is open (revision 17).</summary>
public static class ClaudeDesktopApp
{
    /// <summary>
    /// A taskbar app key (TaskbarWindows: "aumid:" or a lowercase "exe:" path) that is Claude Desktop: the Store package
    /// Claude_pzs8sxrjxfjjc, or the installer's AnthropicClaude. The CLI's claude.exe owns no window and is never it.
    /// </summary>
    public static bool Owns(string appKey) =>
        appKey.StartsWith("aumid:Claude_pzs8sxrjxfjjc!", StringComparison.OrdinalIgnoreCase)
        || appKey.StartsWith("aumid:com.squirrel.AnthropicClaude.", StringComparison.OrdinalIgnoreCase)
        || (appKey.StartsWith("exe:", StringComparison.OrdinalIgnoreCase) && appKey.EndsWith(@"\claude.exe", StringComparison.OrdinalIgnoreCase)
            && (appKey.Contains(@"\windowsapps\claude_", StringComparison.OrdinalIgnoreCase)
                || appKey.Contains(@"\anthropicclaude\", StringComparison.OrdinalIgnoreCase)));

    /// <summary>Open while one of its windows has a taskbar button, minimized too; closed to the tray it is not.</summary>
    public static bool IsOpen(IEnumerable<TaskbarWindow> windows) => windows.Any(w => TaskbarFilter.Shows(w) && Owns(w.AppKey));
}
