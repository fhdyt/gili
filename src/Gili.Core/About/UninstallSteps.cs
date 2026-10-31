namespace Gili.Core.About;

/// <summary>One thing Prepare to uninstall puts back, named for the log.</summary>
public sealed record UninstallStep(string Name, Action Run);

/// <summary>
/// Prepare to uninstall (PRD 9.4, design 5a): every step runs even when one before it fails, so a locked file never
/// leaves the taskbar hidden; each failure goes to <paramref name="failed"/>.
/// </summary>
public static class UninstallSteps
{
    /// <summary>True when every step worked.</summary>
    public static bool Run(IEnumerable<UninstallStep> steps, Action<string, Exception> failed)
    {
        var ok = true;
        foreach (var step in steps)
        {
            try
            {
                step.Run();
            }
            catch (Exception ex)
            {
                ok = false;
                failed(step.Name, ex);
            }
        }
        return ok;
    }
}
