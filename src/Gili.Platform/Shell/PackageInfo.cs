using System.Runtime.InteropServices;
using Windows.Storage;

namespace Gili.Platform.Shell;

/// <summary>Whether Gili runs from its MSIX package (the Store copy, PRD 9.4), and that package's data folder (PRD 10).</summary>
public static class PackageInfo
{
    private const int APPMODEL_ERROR_NO_PACKAGE = 15700;

    public static bool IsPackaged { get; } = Detect();

    /// <summary>The Store copy was started by its StartupTask at sign-in rather than by the user.</summary>
    public static bool StartedByStartupTask()
    {
        if (!IsPackaged) return false;
        try
        {
            return Windows.ApplicationModel.AppInstance.GetActivatedEventArgs()?.Kind == Windows.ApplicationModel.Activation.ActivationKind.StartupTask;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return false;
        }
    }

    /// <summary>The package family name, for the Store copy's page in the Store; null outside a package.</summary>
    public static string? FamilyName() => IsPackaged ? Windows.ApplicationModel.Package.Current.Id.FamilyName : null;

    /// <summary>%LOCALAPPDATA%\Packages\&lt;package&gt;\LocalState, or null outside a package.</summary>
    public static string? LocalFolder() => IsPackaged ? ApplicationData.Current.LocalFolder.Path : null;

    private static bool Detect()
    {
        uint length = 0;
        // With no buffer this answers "too small" inside a package and "no package" outside one.
        return GetCurrentPackageFullName(ref length, IntPtr.Zero) != APPMODEL_ERROR_NO_PACKAGE;
    }

    [DllImport("kernel32.dll")]
    private static extern int GetCurrentPackageFullName(ref uint length, IntPtr name);
}
