using System.Globalization;
using Gili.Core.Alarms;
using Gili.Core.Sound;
using Gili.Core.Time;
using Microsoft.Data.Sqlite;

namespace Gili.Data;

/// <summary>The alarms table (PRD 10): local "HH:mm", the repeat-day bitmask, the sound in lower case, and the next ring in UTC.</summary>
public sealed class AlarmStore(Database db)
{
    public IReadOnlyList<Alarm> All()
    {
        using var command = db.Connection.CreateCommand();
        command.CommandText = "SELECT id, time_of_day, repeat_days, label, sound, enabled, next_fire_at FROM alarms ORDER BY time_of_day, id";
        using var reader = command.ExecuteReader();
        var alarms = new List<Alarm>();
        while (reader.Read())
        {
            alarms.Add(new Alarm(
                reader.GetInt64(0),
                TimeOnly.ParseExact(reader.GetString(1), "HH:mm", CultureInfo.InvariantCulture),
                (Days)reader.GetInt32(2),
                reader.GetString(3),
                Voices.Find(reader.GetString(4)).Name,
                reader.GetInt32(5) != 0,
                reader.IsDBNull(6) ? null : DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)));
        }
        return alarms;
    }

    public Alarm Add(Alarm alarm)
    {
        using var command = Command(alarm,
            "INSERT INTO alarms (time_of_day, repeat_days, label, sound, enabled, next_fire_at) " +
            "VALUES ($time, $days, $label, $sound, $enabled, $next); SELECT last_insert_rowid();");
        return alarm with { Id = (long)command.ExecuteScalar()! };
    }

    public void Update(Alarm alarm)
    {
        using var command = Command(alarm,
            "UPDATE alarms SET time_of_day = $time, repeat_days = $days, label = $label, sound = $sound, " +
            "enabled = $enabled, next_fire_at = $next WHERE id = $id");
        command.ExecuteNonQuery();
    }

    public void Delete(long id)
    {
        using var command = db.Connection.CreateCommand();
        command.CommandText = "DELETE FROM alarms WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    private SqliteCommand Command(Alarm alarm, string sql)
    {
        var command = db.Connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$id", alarm.Id);
        command.Parameters.AddWithValue("$time", alarm.Time.ToString("HH:mm", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$days", (int)alarm.Repeat);
        command.Parameters.AddWithValue("$label", alarm.Label);
        command.Parameters.AddWithValue("$sound", Voices.Find(alarm.Sound).Name.ToLowerInvariant());
        command.Parameters.AddWithValue("$enabled", alarm.Enabled ? 1 : 0);
        command.Parameters.AddWithValue("$next",
            alarm.NextFireAt is { } next ? next.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) : DBNull.Value);
        return command;
    }
}
