namespace Gili.Core;

/// <summary>
/// What a second launch does when Gili already runs (F-BAR-05): opened by hand, it brings up Settings in the running
/// copy; started at login (the Run entry's --autostart, or the Store copy's StartupTask) it just leaves, so having both
/// copies installed never opens Settings at every sign-in (PRD 9.4).
/// </summary>
public static class LaunchRules
{
    public const string AutostartArgument = "--autostart";

    public static bool SignalsRunningCopy(IReadOnlyList<string> args, bool startedByStartupTask) =>
        !startedByStartupTask && !args.Contains(AutostartArgument);

    /// <summary>The Run entry: the quoted path, marked as a login start.</summary>
    public static string RunCommand(string exePath) => $"\"{exePath}\" {AutostartArgument}";
}
