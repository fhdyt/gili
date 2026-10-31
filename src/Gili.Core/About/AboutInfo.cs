namespace Gili.Core.About;

/// <summary>Settings → About's text (PRD 6.5, design 5a).</summary>
public static class AboutInfo
{
    /// <summary>The version as MAJOR.MINOR.PATCH and how this copy was installed: <c>0.1.1 · GitHub</c>.</summary>
    public static string VersionLine(Version version, InstallKind kind) =>
        Format(version) + " · " + kind switch
        {
            InstallKind.Store => "Microsoft Store",
            InstallKind.Installed => "GitHub",
            _ => "Development build",
        };

    /// <summary>MAJOR.MINOR.PATCH, as both release paths number Gili (PRD 9.4).</summary>
    public static string Format(Version version) => $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";

    /// <summary>
    /// F-PUB-07, PRD 9.4: with the Store and GitHub copies both installed only one runs, and the running one's About
    /// says so. A development build is neither, so it never warns.
    /// </summary>
    public static string? AnotherCopyNote(InstallKind kind, bool storeInstalled, bool githubInstalled) =>
        (kind == InstallKind.Installed && storeInstalled) || (kind == InstallKind.Store && githubInstalled)
            ? "Another copy of Gili is installed. Uninstall one of them in Windows Settings › Apps"
            : null;

    /// <summary>The dev package is "Gili"; Partner Center reserves "&lt;publisher&gt;.Gili" (F-PUB-02).</summary>
    public static bool IsGiliPackage(string name) => name == "Gili" || name.EndsWith(".Gili", StringComparison.Ordinal);
}
