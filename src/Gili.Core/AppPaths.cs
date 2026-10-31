namespace Gili.Core;

/// <summary>How this copy of Gili was installed (PRD 9.4): it decides the data folder and how it starts with Windows.</summary>
public enum InstallKind { Development, Installed, Store }

/// <summary>Where Gili keeps settings.json, gili.db, logs and the crash marker (PRD 10).</summary>
public sealed record DataFolders(string Root)
{
    public string Logs => Path.Combine(Root, "logs");
    public string CrashMarker => Path.Combine(Root, "last-crash.txt");

    /// <summary>Files the Shelf keeps itself, one folder per drop (F-ISL-31).</summary>
    public string Shelf => Path.Combine(Root, "shelf");

    /// <summary>Images in the clipboard history, as PNG (F-ISL-32).</summary>
    public string Clipboard => Path.Combine(Root, "clipboard");
}

public static class AppPaths
{
    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public static string InstallDirectory { get; } = Path.Combine(LocalAppData, "Programs", "Gili");

    /// <summary>
    /// Copies of gili.db and settings.json outside every data folder, which a reinstall finds again: uninstalling the
    /// Store copy deletes its package folder, backups included (design 2026-10-31 data tetap). Packages don't
    /// virtualize the profile folder, so the Store and GitHub copies share it.
    /// </summary>
    public static string KeepFolder { get; } = KeepFolderIn(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    public static string KeepFolderIn(string userProfile) => Path.Combine(userProfile, ".gili", "backups");

    /// <summary>The Store package keeps its data in the package's own folder; every other copy in %LOCALAPPDATA%\Gili.</summary>
    public static DataFolders Data(string? packageLocalFolder) => new(packageLocalFolder ?? Path.Combine(LocalAppData, "Gili"));

    /// <summary>A package is always the Store copy; outside one, only the copy under Programs\Gili is installed.</summary>
    public static InstallKind KindOf(string? exePath, bool packaged, string installDirectory) =>
        packaged ? InstallKind.Store
        : exePath is not null && IsUnder(exePath, installDirectory) ? InstallKind.Installed
        : InstallKind.Development;

    public static bool IsUnder(string path, string directory)
    {
        var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(folder, StringComparison.OrdinalIgnoreCase);
    }
}
