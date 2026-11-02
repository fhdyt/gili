using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace Gili.Data;

/// <summary>
/// One copy of gili.db a day in backups\gili-YYYY-MM-DD.db, the latest seven kept (PRD 10). Copies go through
/// SQLite's backup API, so they are consistent even while Gili writes. Other files in the folder are never touched.
/// The keep copies outside the data folder use the same names, refreshed through the day, so a reinstall that lost the
/// data folder finds them (design 2026-10-31 data tetap).
/// </summary>
public static partial class Backups
{
    public const string FolderName = "backups";
    public const int Keep = 7;

    /// <summary>True when today's copy was made now; false when it already exists.</summary>
    public static bool BackUpIfDue(Database db, string folder, DateOnly today)
    {
        Directory.CreateDirectory(folder);
        var path = PathFor(folder, today);
        if (File.Exists(path)) return false;
        Copy(db, path);
        foreach (var old in List(folder).Skip(Keep)) File.Delete(old);
        return true;
    }

    /// <summary>Writes today's copy again, replacing the one made earlier today.</summary>
    public static void Refresh(Database db, string folder, DateOnly today)
    {
        Directory.CreateDirectory(folder);
        Copy(db, PathFor(folder, today));
        foreach (var old in List(folder).Skip(Keep)) File.Delete(old);
    }

    /// <summary>A consistent copy of the open database at <paramref name="path"/>, written aside first so a reader never sees half of it.</summary>
    internal static void Copy(Database db, string path) => Copy(db.Connection, path);

    /// <summary>
    /// The copy leaves the clipboard history out, vacuumed so its rows are not left in free pages: the history lasts
    /// one Windows sign-in and must not live on in seven days of copies (revision 20).
    /// </summary>
    internal static void Copy(SqliteConnection source, string path)
    {
        var temp = path + ".tmp";
        using (var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = temp, Pooling = false }.ToString()))
        {
            target.Open();
            source.BackupDatabase(target);
            using var strip = target.CreateCommand();
            strip.CommandText = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'clipboard_history'";
            if ((long)strip.ExecuteScalar()! > 0)
            {
                strip.CommandText = "DELETE FROM clipboard_history; VACUUM;";
                strip.ExecuteNonQuery();
            }
        }
        File.Move(temp, path, overwrite: true);
    }

    private static string PathFor(string folder, DateOnly day) =>
        Path.Combine(folder, $"gili-{day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.db");

    /// <summary>The backups, newest date first.</summary>
    public static IReadOnlyList<string> List(string? folder) =>
        folder is null || !Directory.Exists(folder) ? [] :
        [.. Directory.GetFiles(folder, "gili-*.db")
            .Where(f => BackupName().IsMatch(Path.GetFileName(f)))
            .OrderByDescending(f => Path.GetFileName(f), StringComparer.Ordinal)];

    [GeneratedRegex(@"^gili-\d{4}-\d{2}-\d{2}\.db$")]
    private static partial Regex BackupName();
}
