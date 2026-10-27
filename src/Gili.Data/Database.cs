using Microsoft.Data.Sqlite;

namespace Gili.Data;

/// <summary>
/// gili.db in the data folder (PRD 10): WAL, so a forced shutdown loses at most the last moment of typing (F-TOOL-09),
/// and migrated to the latest schema on open.
/// </summary>
public sealed class Database : IDisposable
{
    public const string FileName = "gili.db";

    private Database(SqliteConnection connection, string filePath, int version)
    {
        Connection = connection;
        FilePath = filePath;
        Version = version;
    }

    public SqliteConnection Connection { get; }

    public string FilePath { get; }

    public int Version { get; }

    public static Database Open(string folder)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, FileName);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
        connection.Open();
        using (var pragmas = connection.CreateCommand())
        {
            pragmas.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA foreign_keys=ON;";
            pragmas.ExecuteNonQuery();
        }
        return new Database(connection, path, Migrations.Apply(connection));
    }

    public void Dispose() => Connection.Dispose();
}
