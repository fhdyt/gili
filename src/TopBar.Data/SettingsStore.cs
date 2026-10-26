using System.Text.Json;
using System.Text.Json.Serialization;
using TopBar.Core.Settings;

namespace TopBar.Data;

public sealed record SettingsLoadResult(AppSettings Settings, bool WasCorrupt);

/// <summary>settings.json in the app data folder (PRD 10).</summary>
public sealed class SettingsStore(string directory)
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

    /// <summary>Writes to a temp file first so a crash mid-write never leaves a half-written settings.json.</summary>
    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(directory);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
        File.Move(temp, FilePath, overwrite: true);
    }
}
