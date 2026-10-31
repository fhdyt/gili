namespace Gili.Core.About;

/// <summary>Settings → About's text (PRD 6.5, design 5a).</summary>
public static class AboutInfo
{
    /// <summary>The version as MAJOR.MINOR.PATCH and how this copy was installed: <c>0.1.1 · GitHub</c>.</summary>
    public static string VersionLine(Version version, InstallKind kind) =>
        $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)} · " + kind switch
        {
            InstallKind.Store => "Microsoft Store",
            InstallKind.Installed => "GitHub",
            _ => "Development build",
        };
}
