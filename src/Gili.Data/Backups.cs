using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace Gili.Data;

/// <summary>
/// One copy of gili.db a day in backups\gili-YYYY-MM-DD.db, the latest seven kept (PRD 10). Copies go through
/// SQLite's backup API, so they are consistent even while Gili writes. Other files in the folder are never touched.
/// </summary>
public static partial class Backups
{
    public const string FolderName = "backups";
    public const int Keep = 7;

    /// <summary>True when today's copy was made now; false when it already exists.</summary>
    public static bool BackUpIfDue(Database db, string folder, DateOnly today)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"gili-{today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.db");
        if (File.Exists(path)) return false;
        var temp = path + ".tmp";
        using (var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = temp, Pooling = false }.ToString()))
        {
            target.Open();
            db.Connection.BackupDatabase(target);
        }
        File.Move(temp, path);
        foreach (var old in List(folder).Skip(Keep)) File.Delete(old);
        return true;
    }

    /// <summary>The backups, newest date first.</summary>
    public static IReadOnlyList<string> List(string folder) =>
        !Directory.Exists(folder) ? [] :
        [.. Directory.GetFiles(folder, "gili-*.db")
            .Where(f => BackupName().IsMatch(Path.GetFileName(f)))
            .OrderByDescending(f => Path.GetFileName(f), StringComparer.Ordinal)];

    [GeneratedRegex(@"^gili-\d{4}-\d{2}-\d{2}\.db$")]
    private static partial Regex BackupName();
}
