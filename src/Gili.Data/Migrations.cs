using Microsoft.Data.Sqlite;

namespace Gili.Data;

/// <summary>
/// Schema steps in order; a step's version is its index + 1 and is recorded in schema_version once applied (PRD 10).
/// Never edit a released step: add a new one.
/// </summary>
internal static class Migrations
{
    public static IReadOnlyList<string> Steps { get; } =
    [
        // 1: the tables in PRD 10. Times are UTC ISO 8601 text; an alarm's time_of_day is local "HH:mm".
        """
        CREATE TABLE notes (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            body TEXT NOT NULL DEFAULT '',
            color INTEGER NOT NULL DEFAULT 0,
            text_size INTEGER NOT NULL DEFAULT 2,
            sort_order INTEGER NOT NULL,
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL,
            float_x REAL, float_y REAL, float_w REAL, float_h REAL);
        CREATE TABLE todos (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            title TEXT NOT NULL,
            due_date TEXT, remind_at TEXT, done_at TEXT,
            sort_order INTEGER NOT NULL,
            created_at TEXT NOT NULL);
        CREATE TABLE alarms (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            time_of_day TEXT NOT NULL,
            repeat_days INTEGER NOT NULL DEFAULT 0,
            label TEXT NOT NULL DEFAULT '',
            sound TEXT NOT NULL DEFAULT 'radar',
            enabled INTEGER NOT NULL DEFAULT 1,
            next_fire_at TEXT);
        CREATE TABLE timers (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            kind TEXT NOT NULL,
            label TEXT NOT NULL DEFAULT '',
            duration_s INTEGER NOT NULL,
            ends_at TEXT, remaining_s INTEGER,
            pomodoro_phase TEXT, pomodoro_round INTEGER);
        CREATE TABLE stopwatch (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            started_at TEXT,
            accumulated_ms INTEGER NOT NULL DEFAULT 0,
            laps TEXT NOT NULL DEFAULT '[]');
        CREATE TABLE clipboard_history (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            text TEXT NOT NULL,
            source_app TEXT,
            copied_at TEXT NOT NULL);
        CREATE TABLE claude_window (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            started_at TEXT NOT NULL,
            resets_at TEXT NOT NULL,
            source TEXT NOT NULL,
            limit_hit_at TEXT,
            notified INTEGER NOT NULL DEFAULT 0);
        CREATE TABLE shelf_items (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            paths TEXT NOT NULL,
            display_name TEXT NOT NULL,
            size_bytes INTEGER NOT NULL,
            sort_order INTEGER NOT NULL,
            added_at TEXT NOT NULL);
        """,
        // 2: a task's local time, kept apart from remind_at, which only holds a reminder still to come (plan 2e decision 5).
        "ALTER TABLE todos ADD COLUMN due_time TEXT;",
        // 3: the drop zone's column and the folder Gili copies files from a zip or the temp folder into (F-ISL-30, F-ISL-31).
        "ALTER TABLE shelf_items ADD COLUMN mode TEXT NOT NULL DEFAULT 'Copy'; ALTER TABLE shelf_items ADD COLUMN folder TEXT;",
        // 4: images and files in the clipboard history (F-ISL-32).
        "ALTER TABLE clipboard_history ADD COLUMN kind TEXT NOT NULL DEFAULT 'Text'; ALTER TABLE clipboard_history ADD COLUMN name TEXT; " +
        "ALTER TABLE clipboard_history ADD COLUMN width INTEGER NOT NULL DEFAULT 0; ALTER TABLE clipboard_history ADD COLUMN height INTEGER NOT NULL DEFAULT 0;",
        // 5: projects and their tasks on the Projects page (F-ISL-34); days are local "yyyy-MM-dd", a project's tasks go with it.
        """
        CREATE TABLE projects (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL,
            sort_order INTEGER NOT NULL,
            created_at TEXT NOT NULL);
        CREATE TABLE project_tasks (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            project_id INTEGER NOT NULL REFERENCES projects (id) ON DELETE CASCADE,
            title TEXT NOT NULL,
            start_date TEXT NOT NULL,
            end_date TEXT NOT NULL,
            color INTEGER NOT NULL DEFAULT 0,
            done_at TEXT,
            sort_order INTEGER NOT NULL,
            created_at TEXT NOT NULL);
        CREATE INDEX project_tasks_by_project ON project_tasks (project_id, sort_order);
        """,
        // 6: Claude's weekly limit reset, set by hand from Claude Desktop (revision 19); UTC like claude_window.
        "CREATE TABLE claude_weekly (id INTEGER PRIMARY KEY AUTOINCREMENT, resets_at TEXT NOT NULL, notified INTEGER NOT NULL DEFAULT 0);",
    ];

    /// <summary>The schema version the file holds; 0 for a new file.</summary>
    public static int Stored(SqliteConnection connection)
    {
        Execute(connection, null, "CREATE TABLE IF NOT EXISTS schema_version (version INTEGER NOT NULL)");
        return Current(connection);
    }

    public static int Apply(SqliteConnection connection)
    {
        var version = Stored(connection);
        for (var step = version; step < Steps.Count; step++)
        {
            using var transaction = connection.BeginTransaction();
            Execute(connection, transaction, Steps[step]);
            Execute(connection, transaction, $"DELETE FROM schema_version; INSERT INTO schema_version (version) VALUES ({step + 1})");
            transaction.Commit();
            version = step + 1;
        }
        return version;
    }

    private static int Current(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT MAX(version) FROM schema_version";
        return command.ExecuteScalar() is long version ? (int)version : 0;
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction? transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
