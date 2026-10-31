using System.Globalization;
using Gili.Core.Notes;
using Microsoft.Data.Sqlite;

namespace Gili.Data;

/// <summary>The notes table (PRD 10): body, colour and text size, the user's order, UTC times, and a floating note's window in float_*.</summary>
public sealed class NoteStore(Database db)
{
    public IReadOnlyList<Note> All()
    {
        using var command = db.Connection.CreateCommand();
        command.CommandText =
            "SELECT id, body, color, text_size, sort_order, created_at, updated_at, float_x, float_y, float_w, float_h " +
            "FROM notes ORDER BY sort_order, id";
        using var reader = command.ExecuteReader();
        var notes = new List<Note>();
        while (reader.Read())
        {
            notes.Add(new Note(
                reader.GetInt64(0),
                reader.GetString(1),
                NoteStyle.ClampColor(reader.GetInt32(2)),
                Enum.IsDefined((NoteTextSize)reader.GetInt32(3)) ? (NoteTextSize)reader.GetInt32(3) : NoteTextSize.Medium,
                reader.GetInt32(4),
                Instant(reader.GetString(5)),
                Instant(reader.GetString(6)),
                FloatOf(reader)));
        }
        return notes;
    }

    /// <summary>Adds the note above all others and returns it with its id and place.</summary>
    public Note Add(Note note)
    {
        using var first = db.Connection.CreateCommand();
        first.CommandText = "SELECT COALESCE(MIN(sort_order), 1) - 1 FROM notes";
        var placed = note with { SortOrder = Convert.ToInt32(first.ExecuteScalar(), CultureInfo.InvariantCulture) };
        using var command = db.Connection.CreateCommand();
        command.CommandText =
            "INSERT INTO notes (body, color, text_size, sort_order, created_at, updated_at) " +
            "VALUES ($body, $color, $size, $order, $created, $updated); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$body", placed.Body);
        command.Parameters.AddWithValue("$color", placed.Color);
        command.Parameters.AddWithValue("$size", (int)placed.Size);
        command.Parameters.AddWithValue("$order", placed.SortOrder);
        command.Parameters.AddWithValue("$created", Text(placed.CreatedAt));
        command.Parameters.AddWithValue("$updated", Text(placed.UpdatedAt));
        return placed with { Id = (long)command.ExecuteScalar()! };
    }

    /// <summary>Writes the text, colour, size and edit time. The order and the floating position stay as they are.</summary>
    public void Update(Note note)
    {
        using var command = db.Connection.CreateCommand();
        command.CommandText = "UPDATE notes SET body = $body, color = $color, text_size = $size, updated_at = $updated WHERE id = $id";
        command.Parameters.AddWithValue("$id", note.Id);
        command.Parameters.AddWithValue("$body", note.Body);
        command.Parameters.AddWithValue("$color", note.Color);
        command.Parameters.AddWithValue("$size", (int)note.Size);
        command.Parameters.AddWithValue("$updated", Text(note.UpdatedAt));
        command.ExecuteNonQuery();
    }

    /// <summary>Numbers the notes 0..n-1 in the given order, in one transaction.</summary>
    public void Reorder(IReadOnlyList<long> ids)
    {
        using var transaction = db.Connection.BeginTransaction();
        using var command = db.Connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE notes SET sort_order = $order WHERE id = $id";
        var order = command.Parameters.Add("$order", SqliteType.Integer);
        var id = command.Parameters.Add("$id", SqliteType.Integer);
        for (var i = 0; i < ids.Count; i++)
        {
            order.Value = i;
            id.Value = ids[i];
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    /// <summary>Writes where a floating note's window is, or null once it is back in the island.</summary>
    public void SetFloat(long id, NoteRect? rect)
    {
        using var command = db.Connection.CreateCommand();
        command.CommandText = "UPDATE notes SET float_x = $x, float_y = $y, float_w = $w, float_h = $h WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$x", (object?)rect?.X ?? DBNull.Value);
        command.Parameters.AddWithValue("$y", (object?)rect?.Y ?? DBNull.Value);
        command.Parameters.AddWithValue("$w", (object?)rect?.Width ?? DBNull.Value);
        command.Parameters.AddWithValue("$h", (object?)rect?.Height ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    public void Delete(long id)
    {
        using var command = db.Connection.CreateCommand();
        command.CommandText = "DELETE FROM notes WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    /// <summary>Drops notes in the island that are empty or only whitespace (left behind by a crash) and returns how many.</summary>
    public int DeleteBlank()
    {
        using var transaction = db.Connection.BeginTransaction();
        var blank = new List<long>();
        using (var read = db.Connection.CreateCommand())
        {
            read.Transaction = transaction;
            // A floating note may be blank on purpose; it goes once it is back in the island (F-TOOL-12).
            read.CommandText = "SELECT id, body FROM notes WHERE float_x IS NULL OR float_y IS NULL OR float_w IS NULL OR float_h IS NULL";
            using var reader = read.ExecuteReader();
            while (reader.Read())
                if (NoteText.IsBlank(reader.GetString(1))) blank.Add(reader.GetInt64(0));
        }
        using var delete = db.Connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText = "DELETE FROM notes WHERE id = $id";
        var id = delete.Parameters.Add("$id", SqliteType.Integer);
        foreach (var value in blank)
        {
            id.Value = value;
            delete.ExecuteNonQuery();
        }
        transaction.Commit();
        return blank.Count;
    }

    /// <summary>A window only when all four values are there; anything less (a file edited by hand) is a note in the island.</summary>
    private static NoteRect? FloatOf(SqliteDataReader reader)
    {
        for (var i = 7; i <= 10; i++)
            if (reader.IsDBNull(i)) return null;
        return new NoteRect(reader.GetDouble(7), reader.GetDouble(8), reader.GetDouble(9), reader.GetDouble(10));
    }

    private static string Text(DateTimeOffset at) => at.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);

    private static DateTimeOffset Instant(string text) =>
        DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
