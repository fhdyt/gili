using Gili.Core.Claude;

namespace Gili.Platform.Claude;

/// <summary>
/// Claude Code's ~/.claude/settings.json, changed through a backup (settings.json.gili-backup) and a temp file. A file
/// Gili cannot read is never written (F-CLD-01, PRD 11). Shared by the running app and <c>--prepare-uninstall</c>.
/// </summary>
public static class ClaudeSettingsFile
{
    public static string Folder { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");

    private static readonly string SettingsPath = Path.Combine(Folder, "settings.json");

    /// <summary>
    /// True when the file changed. Throws <see cref="ClaudeSettingsException"/> for a file that is not a JSON object,
    /// and IOException or UnauthorizedAccessException when it cannot be read or written.
    /// </summary>
    public static bool Edit(Func<string?, string> change, bool onlyIfExists)
    {
        var exists = File.Exists(SettingsPath);
        if (!exists && onlyIfExists) return false;
        var text = exists ? File.ReadAllText(SettingsPath) : null;
        var next = change(text);
        if (next == text) return false;
        Directory.CreateDirectory(Folder);
        if (exists) File.Copy(SettingsPath, SettingsPath + ".gili-backup", overwrite: true);
        var temp = SettingsPath + ".gili-tmp";
        File.WriteAllText(temp, next);
        File.Move(temp, SettingsPath, overwrite: true);
        return true;
    }

    /// <summary>Takes Gili's hooks out, leaving every other hook; a missing file stays missing.</summary>
    public static bool RemoveHooks() => Edit(text => ClaudeHookConfig.Uninstall(text ?? ""), onlyIfExists: true);
}
