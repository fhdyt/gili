using Gili.Core;
using Gili.Core.About;

namespace Gili.Platform.Shell;

/// <summary>Whether the other release path of Gili is installed too (F-PUB-07, PRD 9.4). Reads only; call it off the UI thread.</summary>
public static class OtherCopies
{
    /// <summary>The Store copy, through the user's package list; a refused query counts as not installed.</summary>
    public static bool StoreInstalled()
    {
        try
        {
            return new Windows.Management.Deployment.PackageManager().FindPackagesForUser(string.Empty)
                .Any(p => AboutInfo.IsGiliPackage(p.Id.Name));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            return false;
        }
    }

    /// <summary>The GitHub copy; the Store copy reads %LOCALAPPDATA% through to the real folder.</summary>
    public static bool GithubInstalled() => File.Exists(Path.Combine(AppPaths.InstallDirectory, "Gili.exe"));
}
