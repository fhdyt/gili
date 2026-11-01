using System.Globalization;
using Gili.Core.Claude;

namespace Gili.Data;

/// <summary>
/// The claude_weekly table (PRD 10, revision 19): the weekly limit's reset set by hand, in UTC. The active one is the newest
/// not yet notified.
/// </summary>
public sealed class ClaudeWeeklyStore(Database db) : IClaudeWeeklyStore
{
    public ClaudeWeeklyReset? Active()
    {
        using var command = db.Connection.CreateCommand();
        command.CommandText = "SELECT id, resets_at, notified FROM claude_weekly WHERE notified = 0 ORDER BY id DESC LIMIT 1";
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;
        return new ClaudeWeeklyReset(reader.GetInt64(0), Time(reader.GetString(1)), reader.GetInt64(2) != 0);
    }

    public ClaudeWeeklyReset Save(ClaudeWeeklyReset reset)
    {
        using var command = db.Connection.CreateCommand();
        if (reset.Id == 0)
        {
            // Only one reset is set at a time: the one set before is closed as this one comes.
            command.CommandText = "UPDATE claude_weekly SET notified = 1 WHERE notified = 0; " +
                "INSERT INTO claude_weekly (resets_at, notified) VALUES ($reset, $notified); SELECT last_insert_rowid();";
        }
        else
        {
            command.CommandText = "UPDATE claude_weekly SET resets_at = $reset, notified = $notified WHERE id = $id";
            command.Parameters.AddWithValue("$id", reset.Id);
        }
        command.Parameters.AddWithValue("$reset", reset.ResetsAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$notified", reset.Notified ? 1 : 0);
        if (reset.Id != 0)
        {
            command.ExecuteNonQuery();
            return reset;
        }
        return reset with { Id = (long)command.ExecuteScalar()! };
    }

    private static DateTimeOffset Time(string text) =>
        DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
}
