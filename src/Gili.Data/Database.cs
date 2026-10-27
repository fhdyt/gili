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

    private Database(SqliteConnection connection, string filePath, int version, OpenOutcome outcome, string? corruptCopy)
    {
        Connection = connection;
        FilePath = filePath;
        Version = version;
        Outcome = outcome;
        CorruptCopy = corruptCopy;
    }

    public SqliteConnection Connection { get; }

    public string FilePath { get; }

    public int Version { get; }

    public OpenOutcome Outcome { get; }

    /// <summary>Where the corrupt file was kept, when there was one.</summary>
    public string? CorruptCopy { get; }

    public static Database Open(string folder)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, FileName);
        if (!File.Exists(path) || TryOpen(path) is not { } connection)
        {
            var corrupt = File.Exists(path) ? SetAside(path) : null;
            var (restored, outcome) = corrupt is null ? (null, OpenOutcome.Normal) : Restore(path, Path.Combine(folder, Backups.FolderName));
            connection = restored ?? TryOpen(path) ?? throw new InvalidOperationException("Could not create " + path);
            return new Database(connection, path, Migrations.Apply(connection), outcome, corrupt);
        }
        return new Database(connection, path, Migrations.Apply(connection), OpenOutcome.Normal, null);
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

    private static (SqliteConnection? Connection, OpenOutcome Outcome) Restore(string path, string backups)
    {
        foreach (var backup in Backups.List(backups))
        {
            File.Copy(backup, path, overwrite: true);
            if (TryOpen(path) is { } connection) return (connection, OpenOutcome.RestoredFromBackup);
            File.Delete(path);
        }
        return (null, OpenOutcome.CreatedNew);
    }
}
