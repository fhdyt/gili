using Gili.Core.Settings;
using Windows.ApplicationModel;

namespace Gili.Platform.Shell;

/// <summary>
/// Start with Windows for the Store package: the manifest's StartupTask instead of the Run key, which MSIX virtualises
/// (PRD 9.4). Task Manager has the last word; once the user turns it off there, Gili never turns it back on.
/// </summary>
public static class PackagedStartup
{
    private const string TaskId = "GiliStartup";

    public static async Task<StartupState> ReadAsync() => Map((await StartupTask.GetAsync(TaskId)).State);

    public static async Task<StartupState> SetAsync(bool on)
    {
        var task = await StartupTask.GetAsync(TaskId);
        var state = Map(task.State);
        if (!StartupRules.CanChange(state) || state == (on ? StartupState.On : StartupState.Off)) return state;
        if (on) return Map(await task.RequestEnableAsync());
        task.Disable();
        return Map(task.State);
    }

    private static StartupState Map(StartupTaskState state) => state switch
    {
        StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy => StartupState.On,
        StartupTaskState.DisabledByUser => StartupState.BlockedByUser,
        StartupTaskState.DisabledByPolicy => StartupState.BlockedByPolicy,
        _ => StartupState.Off,
    };
}
