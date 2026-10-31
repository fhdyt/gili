using System.Globalization;
using Gili.Core.Claude;
using Microsoft.Data.Sqlite;

namespace Gili.Data;

/// <summary>
/// The claude_window table (PRD 10): one row per 5-hour window, times in UTC, source "estimated" or "official". The active
/// window is the newest one not yet notified.
/// </summary>
public sealed class ClaudeWindowStore(Database db) : IClaudeWindowStore
{
    public ClaudeUsageWindow? Active()
    {
        using var command = db.Connection.CreateCommand();
        command.CommandText = "SELECT id, started_at, resets_at, source, limit_hit_at, notified FROM claude_window WHERE notified = 0 ORDER BY id DESC LIMIT 1";
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;
        return new ClaudeUsageWindow(
            reader.GetInt64(0),
            Time(reader.GetString(1)),
            Time(reader.GetString(2)),
            reader.GetString(3) == "official" ? ClaudeWindowSource.Official : ClaudeWindowSource.Estimated,
            reader.IsDBNull(4) ? null : Time(reader.GetString(4)),
            reader.GetInt64(5) != 0);
    }

    public ClaudeUsageWindow Save(ClaudeUsageWindow window)
    {
        using var command = db.Connection.CreateCommand();
        if (window.Id == 0)
        {
            // Only one window runs at a time: an older one left open is closed as this one starts.
            command.CommandText = "UPDATE claude_window SET notified = 1 WHERE notified = 0; " +
                "INSERT INTO claude_window (started_at, resets_at, source, limit_hit_at, notified) VALUES ($start, $reset, $source, $hit, $notified); " +
                "SELECT last_insert_rowid();";
        }
        else
        {
            command.CommandText = "UPDATE claude_window SET started_at = $start, resets_at = $reset, source = $source, limit_hit_at = $hit, " +
                "notified = $notified WHERE id = $id";
            command.Parameters.AddWithValue("$id", window.Id);
        }
        command.Parameters.AddWithValue("$start", Text(window.StartedAt));
        command.Parameters.AddWithValue("$reset", Text(window.ResetsAt));
        command.Parameters.AddWithValue("$source", window.Source == ClaudeWindowSource.Official ? "official" : "estimated");
        command.Parameters.AddWithValue("$hit", window.LimitHitAt is { } hit ? Text(hit) : DBNull.Value);
        command.Parameters.AddWithValue("$notified", window.Notified ? 1 : 0);
        if (window.Id != 0)
        {
            command.ExecuteNonQuery();
            return window;
        }
        return window with { Id = (long)command.ExecuteScalar()! };
    }

    private static string Text(DateTimeOffset time) => time.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset Time(string text) =>
        DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
}
