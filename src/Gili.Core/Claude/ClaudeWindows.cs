namespace Gili.Core.Claude;

/// <summary>A process's parent, its start time (UTC ticks) and its program's file name.</summary>
public readonly record struct ProcessFacts(int Parent, long StartTicks, string Name);

/// <summary>
/// Finds a session's window (PRD 6.3 "Jendela sesi", F-CLD-06): from Claude Code's process up through its parents to the
/// first one with a visible top-level window, Claude Desktop for a Desktop session or the terminal for a CLI one. A parent
/// that started after its child has a reused PID and is not the real parent; Explorer starts everything from the Start
/// menu, so its windows never count.
/// </summary>
public static class ClaudeWindows
{
    private const int MaxDepth = 12;

    /// <returns>The window and the process that owns it; null when none was found.</returns>
    public static (nint Window, int Pid)? Find(int pid, Func<int, ProcessFacts?> facts, Func<int, nint> windowOf)
    {
        if (pid <= 0) return null;
        var current = pid;
        var info = facts(current);
        for (var depth = 0; depth < MaxDepth && info is { } self; depth++)
        {
            if (string.Equals(self.Name, "explorer.exe", StringComparison.OrdinalIgnoreCase)) return null;
            var window = windowOf(current);
            if (window != 0) return (window, current);
            // A start time of 0 could not be read (an elevated process): the link is trusted rather than lost.
            if (self.Parent <= 0 || facts(self.Parent) is not { } parent || (self.StartTicks > 0 && parent.StartTicks > self.StartTicks)) return null;
            current = self.Parent;
            info = parent;
        }
        return null;
    }
}
