using System.Text.Json;
using Gili.Core.Taskbar;

namespace Gili.Data;

/// <summary>
/// taskbar-state.json in the data folder: the taskbar's auto-hide state from before Gili hid it (F-BAR-11). Written
/// through a temp file so a crash never leaves half of it; an unreadable file counts as missing.
/// </summary>
public sealed class TaskbarStateFile(string directory) : ITaskbarStateFile
{
    private sealed record State(bool AutoHide);

    public string FilePath { get; } = Path.Combine(directory, "taskbar-state.json");

    public bool? Read()
    {
        try
        {
            return File.Exists(FilePath) ? JsonSerializer.Deserialize<State>(File.ReadAllText(FilePath))?.AutoHide : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Write(bool autoHide)
    {
        Directory.CreateDirectory(directory);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(new State(autoHide)));
        File.Move(temp, FilePath, overwrite: true);
    }

    public void Delete() => File.Delete(FilePath);
}
