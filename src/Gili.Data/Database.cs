using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Gili.Data;

/// <summary>How gili.db opened: as it was, restored from a backup, or new because nothing usable was left (PRD 11).</summary>
public enum OpenOutcome { Normal, RestoredFromBackup, CreatedNew }

/// <summary>
/// gili.db in the data folder (PRD 10): WAL with synchronous=FULL, so a forced shutdown loses at most the last moment of
/// typing (F-TOOL-09). With NORMAL, WAL can lose commits made since the last checkpoint on power loss. The file is migrated
/// to the latest schema on open. A corrupt file is kept aside and replaced by the latest good backup.
/// </summary>
public sealed class Database : IDisposable
{
    public const string FileName = "gili.db";

    private Database(SqliteConnection connection, string filePath, int version, OpenOutcome outcome, string? corruptCopy,
        string? restoredFrom, string? migrationCopy)
    {
        Connection = connection;
        FilePath = filePath;
        Version = version;
        Outcome = outcome;
        CorruptCopy = corruptCopy;
        RestoredFrom = restoredFrom;
        MigrationCopy = migrationCopy;
    }

    public SqliteConnection Connection { get; }

    public string FilePath { get; }

    public int Version { get; }

    public OpenOutcome Outcome { get; }

    /// <summary>Where the corrupt file was kept, when there was one.</summary>
    public string? CorruptCopy { get; }

    /// <summary>The backup or keep copy the file came from, when it was restored.</summary>
    public string? RestoredFrom { get; }

    /// <summary>The copy made before an older schema was migrated, when there was one.</summary>
    public string? MigrationCopy { get; }

    /// <summary>
    /// Opens gili.db in <paramref name="folder"/>. A missing file, as after a reinstall, comes from the newest copy in
    /// <paramref name="keepFolder"/>; a corrupt one from the daily backups, then the keep copies (design 2026-10-31
    /// data tetap). An older schema is copied to backups\gili-before-v&lt;N&gt;.db before it is migrated.
    /// </summary>
    public static Database Open(string folder, string? keepFolder = null)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, FileName);
        var outcome = OpenOutcome.Normal;
        string? corrupt = null, restoredFrom = null;
        var connection = File.Exists(path) ? TryOpen(path) : null;
        if (connection is null)
        {
            if (File.Exists(path)) corrupt = SetAside(path);
            IEnumerable<string> sources = corrupt is null
                ? Backups.List(keepFolder)
                : [.. Backups.List(Path.Combine(folder, Backups.FolderName)), .. Backups.List(keepFolder)];
            (connection, restoredFrom) = Restore(path, sources);
            if (connection is not null) outcome = OpenOutcome.RestoredFromBackup;
            else if (corrupt is not null) outcome = OpenOutcome.CreatedNew;
            connection ??= TryOpen(path) ?? throw new InvalidOperationException("Could not create " + path);
        }
        var migrationCopy = CopyBeforeMigration(connection, folder);
        return new Database(connection, path, Migrations.Apply(connection), outcome, corrupt, restoredFrom, migrationCopy);
    }

    /// <summary>A file written by an older Gili is copied as it was, so a faulty migration step never costs the notes.</summary>
    private static string? CopyBeforeMigration(SqliteConnection connection, string folder)
    {
        var stored = Migrations.Stored(connection);
        if (stored == 0 || stored >= Migrations.Steps.Count) return null;
        var backups = Path.Combine(folder, Backups.FolderName);
        Directory.CreateDirectory(backups);
        var copy = Path.Combine(backups, $"gili-before-v{Migrations.Steps.Count}.db");
        Backups.Copy(connection, copy);
        return copy;
    }

    public void Dispose() => Connection.Dispose();

    /// <summary>An open, healthy connection with Gili's pragmas, or null when the file is not a usable database.</summary>
    private static SqliteConnection? TryOpen(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
        try
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA quick_check";
            if (command.ExecuteScalar() as string != "ok") throw new SqliteException("quick_check failed", 11);
            command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; PRAGMA foreign_keys=ON;";
            command.ExecuteNonQuery();
            return connection;
        }
        catch (SqliteException)
        {
            connection.Dispose();
            SqliteConnection.ClearPool(connection);
            return null;
        }
    }

    /// <summary>Keeps the broken file as gili.corrupt-yyyyMMdd-HHmmss.db and drops its WAL side files.</summary>
    private static string SetAside(string path)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var target = Path.Combine(Path.GetDirectoryName(path)!, $"gili.corrupt-{stamp}.db");
        File.Move(path, target, overwrite: true);
        foreach (var side in new[] { path + "-wal", path + "-shm" }) File.Delete(side);
        return target;
    }

    private static (SqliteConnection? Connection, string? Source) Restore(string path, IEnumerable<string> sources)
    {
        foreach (var backup in sources)
        {
            File.Copy(backup, path, overwrite: true);
            if (TryOpen(path) is { } connection) return (connection, backup);
            File.Delete(path);
        }
        return (null, null);
    }
}
