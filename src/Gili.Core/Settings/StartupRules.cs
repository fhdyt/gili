namespace Gili.Core.Settings;

/// <summary>The Store package's StartupTask as Settings sees it (PRD 9.4).</summary>
public enum StartupState { On, Off, BlockedByUser, BlockedByPolicy }

public static class StartupRules
{
    /// <summary>Once switched off in Task Manager, only the user can turn it back on there.</summary>
    public static bool CanChange(StartupState state) => state is StartupState.On or StartupState.Off;

    public static string? Note(StartupState state) => state switch
    {
        StartupState.BlockedByUser => "Turned off in Task Manager › Startup apps",
        _ => null,
    };
}
