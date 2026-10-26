namespace Gili.Core;

public static class AppPaths
{
    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public static string Root { get; } = Path.Combine(LocalAppData, "Gili");
    public static string Logs { get; } = Path.Combine(Root, "logs");
    public static string CrashMarker { get; } = Path.Combine(Root, "last-crash.txt");
    public static string InstallDirectory { get; } = Path.Combine(LocalAppData, "Programs", "Gili");

    public static bool IsUnder(string path, string directory)
    {
        var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(folder, StringComparison.OrdinalIgnoreCase);
    }
}
