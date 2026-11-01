using Gili.Core.Apps;
using Gili.Core.Claude;

namespace Gili.Core.Tests.Claude;

/// <summary>Claude's sessions and 5-hour timer show only while Claude Desktop is open (revision 17).</summary>
public sealed class ClaudeDesktopAppTests
{
    [Theory]
    [InlineData("aumid:Claude_pzs8sxrjxfjjc!Claude", true)]
    [InlineData("aumid:com.squirrel.AnthropicClaude.claude", true)]
    [InlineData(@"exe:c:\program files\windowsapps\claude_4.4212.0.0_x64__pzs8sxrjxfjjc\app\claude.exe", true)]
    [InlineData(@"exe:c:\users\a\appdata\local\anthropicclaude\app-0.9.3\claude.exe", true)]
    // The CLI's own claude.exe, and apps that only mention Claude, are not Claude Desktop.
    [InlineData(@"exe:c:\users\a\appdata\roaming\claude\claude-code\2.1.309\9078b9c1d466\claude.exe", false)]
    [InlineData(@"exe:c:\users\a\.local\bin\claude.exe", false)]
    [InlineData("aumid:Microsoft.WindowsTerminal_8wekyb3d8bbwe!App", false)]
    [InlineData("aumid:ClaudeNotes_abc!App", false)]
    public void Claude_desktop_is_known_by_its_app_key(string key, bool desktop) => Assert.Equal(desktop, ClaudeDesktopApp.Owns(key));

    [Fact]
    public void Claude_desktop_is_open_while_a_window_of_it_has_a_taskbar_button()
    {
        static TaskbarWindow W(string key, bool visible = true, bool tool = false) => new(1, key, visible, tool, false, false, false, false);
        Assert.True(ClaudeDesktopApp.IsOpen([W("aumid:Chrome"), W("aumid:Claude_pzs8sxrjxfjjc!Claude")]));
        // Closed to the tray: the process lives on, but no window shows on the taskbar.
        Assert.False(ClaudeDesktopApp.IsOpen([W("aumid:Claude_pzs8sxrjxfjjc!Claude", visible: false)]));
        Assert.False(ClaudeDesktopApp.IsOpen([W("aumid:Claude_pzs8sxrjxfjjc!Claude", tool: true)]));
        Assert.False(ClaudeDesktopApp.IsOpen([]));
    }
}
