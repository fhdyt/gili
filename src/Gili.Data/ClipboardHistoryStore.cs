using System.Globalization;
using Gili.Core.Clipboard;

namespace Gili.Data;

/// <summary>The clipboard_history table (PRD 10): text, the app it came from, copied_at in UTC.</summary>
public sealed class ClipboardHistoryStore(Database db) : IClipboardHistoryStore
{
    public IReadOnlyList<ClipboardEntry> All()
    {
        using var command = db.Connection.CreateCommand();
        command.CommandText = "SELECT id, text, source_app, copied_at FROM clipboard_history ORDER BY copied_at DESC, id DESC";
        using var reader = command.ExecuteReader();
        var items = new List<ClipboardEntry>();
        while (reader.Read())
        {
            items.Add(new ClipboardEntry(reader.GetInt64(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2),
                DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)));
        }
        return items;
    }

    public ClipboardEntry Save(ClipboardEntry entry)
    {
        using var command = db.Connection.CreateCommand();
        command.CommandText = entry.Id == 0
            ? "INSERT INTO clipboard_history (text, source_app, copied_at) VALUES ($text, $app, $at); SELECT last_insert_rowid();"
            : "UPDATE clipboard_history SET text = $text, source_app = $app, copied_at = $at WHERE id = $id";
        command.Parameters.AddWithValue("$id", entry.Id);
        command.Parameters.AddWithValue("$text", entry.Text);
        command.Parameters.AddWithValue("$app", (object?)entry.SourceApp ?? DBNull.Value);
        command.Parameters.AddWithValue("$at", entry.CopiedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        if (entry.Id != 0)
        {
            command.ExecuteNonQuery();
            return entry;
        }
        return entry with { Id = (long)command.ExecuteScalar()! };
    }

    public void Delete(long id)
    {
        using var command = db.Connection.CreateCommand();
        command.CommandText = "DELETE FROM clipboard_history WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void Clear()
    {
        using var command = db.Connection.CreateCommand();
        command.CommandText = "DELETE FROM clipboard_history";
        command.ExecuteNonQuery();
    }
}
