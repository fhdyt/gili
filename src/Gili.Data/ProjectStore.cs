using System.Globalization;
using Gili.Core.Notes;
using Gili.Core.Projects;
using Microsoft.Data.Sqlite;

namespace Gili.Data;

/// <summary>
/// The projects and project_tasks tables (PRD 10, F-ISL-34): a task's days as local "yyyy-MM-dd", its done time in UTC.
/// Deleting a project deletes its tasks (ON DELETE CASCADE).
/// </summary>
public sealed class ProjectStore(Database db)
{
    private const string DayFormat = "yyyy-MM-dd";

    public IReadOnlyList<Project> Projects()
    {
        using var command = db.Connection.CreateCommand();
        command.CommandText = "SELECT id, name, sort_order, created_at FROM projects ORDER BY sort_order, id";
        using var reader = command.ExecuteReader();
        var projects = new List<Project>();
        while (reader.Read()) projects.Add(new Project(reader.GetInt64(0), reader.GetString(1), reader.GetInt32(2), Instant(reader, 3)!.Value));
        return projects;
    }

    /// <summary>
    /// Every task of every project, in order. A row whose days can't be read (edited by hand) is skipped, an end before its
    /// start is swapped, and a colour outside 0-7 is Default.
    /// </summary>
    public IReadOnlyList<ProjectTask> Tasks()
    {
        using var command = db.Connection.CreateCommand();
        command.CommandText =
            "SELECT id, project_id, title, start_date, end_date, color, done_at, sort_order, created_at FROM project_tasks ORDER BY project_id, sort_order, id";
        using var reader = command.ExecuteReader();
        var tasks = new List<ProjectTask>();
        while (reader.Read())
        {
            if (!Day(reader.GetString(3), out var start) || !Day(reader.GetString(4), out var end)) continue;
            if (end < start) (start, end) = (end, start);
            tasks.Add(new ProjectTask(
                reader.GetInt64(0),
                reader.GetInt64(1),
                reader.GetString(2),
                start,
                end,
                NoteStyle.ClampColor(reader.GetInt32(5)),
                Instant(reader, 6),
                reader.GetInt32(7),
                Instant(reader, 8)!.Value));
        }
        return tasks;
    }

    /// <summary>Adds the project at the end of the list and returns it with its id and place.</summary>
    public Project AddProject(Project project)
    {
        var placed = project with { SortOrder = Next("SELECT COALESCE(MAX(sort_order), -1) + 1 FROM projects", null) };
        using var command = db.Connection.CreateCommand();
        command.CommandText = "INSERT INTO projects (name, sort_order, created_at) VALUES ($name, $order, $created); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$name", placed.Name);
        command.Parameters.AddWithValue("$order", placed.SortOrder);
        command.Parameters.AddWithValue("$created", Text(placed.CreatedAt));
        return placed with { Id = (long)command.ExecuteScalar()! };
    }

    public void RenameProject(long id, string name)
    {
        using var command = db.Connection.CreateCommand();
        command.CommandText = "UPDATE projects SET name = $name WHERE id = $id";
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void DeleteProject(long id)
    {
        using var command = db.Connection.CreateCommand();
        command.CommandText = "DELETE FROM projects WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    /// <summary>Adds the task as its project's last row and returns it with its id and place.</summary>
    public ProjectTask AddTask(ProjectTask task)
    {
        var placed = task with { SortOrder = Next("SELECT COALESCE(MAX(sort_order), -1) + 1 FROM project_tasks WHERE project_id = $project", task.ProjectId) };
        using var command = Command(placed,
            "INSERT INTO project_tasks (project_id, title, start_date, end_date, color, done_at, sort_order, created_at) " +
            "VALUES ($project, $title, $start, $end, $color, $done, $order, $created); SELECT last_insert_rowid();");
        return placed with { Id = (long)command.ExecuteScalar()! };
    }

    public void UpdateTask(ProjectTask task)
    {
        using var command = Command(task,
            "UPDATE project_tasks SET project_id = $project, title = $title, start_date = $start, end_date = $end, color = $color, " +
            "done_at = $done, sort_order = $order, created_at = $created WHERE id = $id");
        command.ExecuteNonQuery();
    }

    public void DeleteTask(long id)
    {
        using var command = db.Connection.CreateCommand();
        command.CommandText = "DELETE FROM project_tasks WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    private int Next(string sql, long? project)
    {
        using var command = db.Connection.CreateCommand();
        command.CommandText = sql;
        if (project is { } id) command.Parameters.AddWithValue("$project", id);
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private SqliteCommand Command(ProjectTask task, string sql)
    {
        var command = db.Connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$id", task.Id);
        command.Parameters.AddWithValue("$project", task.ProjectId);
        command.Parameters.AddWithValue("$title", task.Title);
        command.Parameters.AddWithValue("$start", task.Start.ToString(DayFormat, CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$end", task.End.ToString(DayFormat, CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$color", task.Color);
        command.Parameters.AddWithValue("$done", task.DoneAt is { } done ? Text(done) : DBNull.Value);
        command.Parameters.AddWithValue("$order", task.SortOrder);
        command.Parameters.AddWithValue("$created", Text(task.CreatedAt));
        return command;
    }

    private static bool Day(string text, out DateOnly day) =>
        DateOnly.TryParseExact(text, DayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out day);

    private static string Text(DateTimeOffset at) => at.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);

    private static DateTimeOffset? Instant(SqliteDataReader reader, int column) =>
        reader.IsDBNull(column) ? null : DateTimeOffset.Parse(reader.GetString(column), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
