using System.Text.Json;
using System.Text.Json.Serialization;
using Gili.Core.Settings;

namespace Gili.Data;

public sealed record SettingsLoadResult(AppSettings Settings, bool WasCorrupt);

/// <summary>
/// settings.json in the app data folder (PRD 10). With a keep folder, every save also refreshes the copy there, so a
/// reinstall brings back even the last change (design 2026-10-31 data tetap).
/// </summary>
public sealed class SettingsStore(string directory, string? keepFolder = null)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public string FilePath { get; } = Path.Combine(directory, "settings.json");

    /// <summary>A missing file gives defaults. A file that isn't valid JSON is renamed to settings.json.bak and defaults are used (PRD 11).</summary>
    public SettingsLoadResult Load()
    {
        if (!File.Exists(FilePath)) return new SettingsLoadResult(new AppSettings(), WasCorrupt: false);
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options)
                ?? throw new JsonException("settings.json holds null");
            return new SettingsLoadResult(settings.Normalize(), WasCorrupt: false);
        }
        catch (JsonException)
        {
            File.Move(FilePath, FilePath + ".bak", overwrite: true);
            return new SettingsLoadResult(new AppSettings(), WasCorrupt: true);
        }
    }

    /// <summary>
    /// Puts the keep copy in place when there is no settings.json yet, as after a reinstall (design 2026-10-31 data
    /// tetap). An existing file is never replaced.
    /// </summary>
    public bool RestoreFrom(string folder)
    {
        var copy = Path.Combine(folder, Path.GetFileName(FilePath));
        if (File.Exists(FilePath) || !File.Exists(copy)) return false;
        Directory.CreateDirectory(directory);
        File.Copy(copy, FilePath);
        return true;
    }

    /// <summary>Copies settings.json into the keep folder, through a temp file; nothing happens before the first save.</summary>
    public static void CopyTo(string settingsPath, string folder)
    {
        if (!File.Exists(settingsPath)) return;
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, Path.GetFileName(settingsPath));
        File.Copy(settingsPath, target + ".tmp", overwrite: true);
        File.Move(target + ".tmp", target, overwrite: true);
    }

    /// <summary>Writes to a temp file first so a crash mid-write never leaves a half-written settings.json.</summary>
    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(directory);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
        File.Move(temp, FilePath, overwrite: true);
        if (keepFolder is not null) CopyTo(FilePath, keepFolder);
    }
}
