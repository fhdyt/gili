namespace TopBar.Core.Diagnostics;

/// <summary>PRD 11: restart once after a crash; a second crash within a minute stops the app.</summary>
public static class CrashPolicy
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static bool ShouldRestart(DateTimeOffset? previousCrash, DateTimeOffset now) =>
        previousCrash is null || now - previousCrash.Value > Window;
}
