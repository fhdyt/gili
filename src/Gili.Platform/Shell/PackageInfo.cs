using System.Runtime.InteropServices;
using Windows.Storage;

namespace Gili.Platform.Shell;

/// <summary>Whether Gili runs from its MSIX package (the Store copy, PRD 9.4), and that package's data folder (PRD 10).</summary>
public static class PackageInfo
{
    private const int APPMODEL_ERROR_NO_PACKAGE = 15700;

    public static bool IsPackaged { get; } = Detect();

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
