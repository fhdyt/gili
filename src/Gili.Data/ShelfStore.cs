using System.Globalization;
using System.Text.Json;
using Gili.Core.Shelf;
using Microsoft.Data.Sqlite;

namespace Gili.Data;

/// <summary>The shelf_items table (PRD 10): the paths as a JSON array, added_at in UTC, the column and Gili's own folder.</summary>
public sealed class ShelfStore(Database db) : IShelfStore
{
    public IReadOnlyList<ShelfItem> All()
    {
        using var command = db.Connection.CreateCommand();
        command.CommandText = "SELECT id, paths, display_name, size_bytes, sort_order, added_at, mode, folder FROM shelf_items ORDER BY sort_order, id";
        using var reader = command.ExecuteReader();
        var items = new List<ShelfItem>();
        while (reader.Read())
        {
            items.Add(new ShelfItem(
                reader.GetInt64(0),
                JsonSerializer.Deserialize<string[]>(reader.GetString(1)) ?? [],
                reader.GetString(2),
                reader.GetInt64(3),
                reader.GetInt32(4),
                DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
                Enum.TryParse<ShelfMode>(reader.GetString(6), out var mode) ? mode : ShelfMode.Copy,
                reader.IsDBNull(7) ? null : reader.GetString(7)));
        }
        return items;
    }

    public ShelfItem Add(ShelfItem item)
    {
        using var command = Command(item,
            "INSERT INTO shelf_items (paths, display_name, size_bytes, sort_order, added_at, mode, folder) " +
            "VALUES ($paths, $name, $size, $order, $added, $mode, $folder); SELECT last_insert_rowid();");
        return item with { Id = (long)command.ExecuteScalar()! };
    }

    public void Update(ShelfItem item)
    {
        using var command = Command(item,
            "UPDATE shelf_items SET paths = $paths, display_name = $name, size_bytes = $size, sort_order = $order, added_at = $added, " +
            "mode = $mode, folder = $folder WHERE id = $id");
        command.ExecuteNonQuery();
    }

    public void Delete(long id)
    {
        using var command = db.Connection.CreateCommand();
        command.CommandText = "DELETE FROM shelf_items WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void Clear()
    {
        using var command = db.Connection.CreateCommand();
        command.CommandText = "DELETE FROM shelf_items";
        command.ExecuteNonQuery();
    }

    private SqliteCommand Command(ShelfItem item, string sql)
    {
        var command = db.Connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$id", item.Id);
        command.Parameters.AddWithValue("$paths", JsonSerializer.Serialize(item.Paths));
        command.Parameters.AddWithValue("$name", item.DisplayName);
        command.Parameters.AddWithValue("$size", item.SizeBytes);
        command.Parameters.AddWithValue("$order", item.SortOrder);
        command.Parameters.AddWithValue("$added", item.AddedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$mode", item.Mode.ToString());
        command.Parameters.AddWithValue("$folder", (object?)item.Folder ?? DBNull.Value);
        return command;
    }
}
