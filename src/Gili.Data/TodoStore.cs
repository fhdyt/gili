using System.Globalization;
using Gili.Core.Todos;
using Microsoft.Data.Sqlite;

namespace Gili.Data;

/// <summary>The todos table (PRD 10): the due day "yyyy-MM-dd" and local "HH:mm", the pending reminder and done time in UTC.</summary>
public sealed class TodoStore(Database db)
{
    public IReadOnlyList<Todo> All()
    {
        using var command = db.Connection.CreateCommand();
        command.CommandText = "SELECT id, title, due_date, due_time, remind_at, done_at, sort_order, created_at FROM todos ORDER BY sort_order, id";
        using var reader = command.ExecuteReader();
        var todos = new List<Todo>();
        while (reader.Read())
        {
            todos.Add(new Todo(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : DateOnly.ParseExact(reader.GetString(2), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                reader.IsDBNull(3) ? null : TimeOnly.ParseExact(reader.GetString(3), "HH:mm", CultureInfo.InvariantCulture),
                Instant(reader, 4),
                Instant(reader, 5),
                reader.GetInt32(6),
                Instant(reader, 7)!.Value));
        }
        return todos;
    }

    /// <summary>Adds the task at the end of the list and returns it with its id and place.</summary>
    public Todo Add(Todo todo)
    {
        using var last = db.Connection.CreateCommand();
        last.CommandText = "SELECT COALESCE(MAX(sort_order), -1) + 1 FROM todos";
        var placed = todo with { SortOrder = Convert.ToInt32(last.ExecuteScalar(), CultureInfo.InvariantCulture) };
        using var command = Command(placed,
            "INSERT INTO todos (title, due_date, due_time, remind_at, done_at, sort_order, created_at) " +
            "VALUES ($title, $date, $time, $remind, $done, $order, $created); SELECT last_insert_rowid();");
        return placed with { Id = (long)command.ExecuteScalar()! };
    }

    public void Update(Todo todo)
    {
        using var command = Command(todo,
            "UPDATE todos SET title = $title, due_date = $date, due_time = $time, remind_at = $remind, done_at = $done, " +
            "sort_order = $order, created_at = $created WHERE id = $id");
        command.ExecuteNonQuery();
    }

    public void Delete(long id)
    {
        using var command = db.Connection.CreateCommand();
        command.CommandText = "DELETE FROM todos WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    private SqliteCommand Command(Todo todo, string sql)
    {
        var command = db.Connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$id", todo.Id);
        command.Parameters.AddWithValue("$title", todo.Title);
        command.Parameters.AddWithValue("$date", (object?)todo.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? DBNull.Value);
        command.Parameters.AddWithValue("$time", (object?)todo.DueTime?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? DBNull.Value);
        command.Parameters.AddWithValue("$remind", Text(todo.RemindAt));
        command.Parameters.AddWithValue("$done", Text(todo.DoneAt));
        command.Parameters.AddWithValue("$order", todo.SortOrder);
        command.Parameters.AddWithValue("$created", Text(todo.CreatedAt));
        return command;
    }

    private static object Text(DateTimeOffset? at) =>
        at is { } value ? value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) : DBNull.Value;

    private static DateTimeOffset? Instant(SqliteDataReader reader, int column) =>
        reader.IsDBNull(column) ? null : DateTimeOffset.Parse(reader.GetString(column), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
