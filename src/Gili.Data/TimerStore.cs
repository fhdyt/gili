using System.Globalization;
using System.Text.Json;
using Gili.Core.Timers;
using Microsoft.Data.Sqlite;

namespace Gili.Data;

/// <summary>
/// The island's timer, pomodoro and stopwatch in gili.db (PRD 10), one row each, plus the labeled timers of Tools (rows
/// with a non-empty label). Times are UTC; a paused timer keeps whole seconds left.
/// </summary>
public sealed class TimerStore(Database db)
{
    private const string Island = "";

    public CountdownTimer LoadTimer() =>
        Read("SELECT duration_s, ends_at, remaining_s FROM timers WHERE kind = 'timer' AND label = '' LIMIT 1",
            reader => reader.Read() ? ReadTimer(reader, 0) : CountdownTimer.Default);

    public void SaveTimer(CountdownTimer timer) => Upsert("timer", timer, null, null);

    /// <summary>The labeled timers of Tools, oldest first. The island's own row (empty label) is never included.</summary>
    public IReadOnlyList<LabeledTimer> LoadLabeled() =>
        Read("SELECT id, label, duration_s, ends_at, remaining_s FROM timers WHERE kind = 'timer' AND label <> '' ORDER BY id", reader =>
        {
            var timers = new List<LabeledTimer>();
            while (reader.Read()) timers.Add(new LabeledTimer(reader.GetInt64(0), reader.GetString(1), ReadTimer(reader, 2)));
            return timers;
        });

    /// <summary>Adds a labeled timer (the label is cleaned first, so it is never empty) and returns it with its id.</summary>
    public LabeledTimer AddLabeled(string label, CountdownTimer clock)
    {
        var clean = LabeledTimer.CleanLabel(label);
        using var command = db.Connection.CreateCommand();
        command.CommandText =
            "INSERT INTO timers (kind, label, duration_s, ends_at, remaining_s) VALUES ('timer', $label, $duration, $ends, $remaining); " +
            "SELECT last_insert_rowid();";
        Bind(command, clean, clock);
        return new LabeledTimer((long)command.ExecuteScalar()!, clean, clock);
    }

    public void UpdateLabeled(LabeledTimer timer)
    {
        using var command = db.Connection.CreateCommand();
        command.CommandText =
            "UPDATE timers SET label = $label, duration_s = $duration, ends_at = $ends, remaining_s = $remaining " +
            "WHERE id = $id AND kind = 'timer' AND label <> ''";
        Bind(command, LabeledTimer.CleanLabel(timer.Label), timer.Clock);
        command.Parameters.AddWithValue("$id", timer.Id);
        command.ExecuteNonQuery();
    }

    public void DeleteLabeled(long id)
    {
        using var command = db.Connection.CreateCommand();
        command.CommandText = "DELETE FROM timers WHERE id = $id AND kind = 'timer' AND label <> ''";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    /// <summary>The saved pomodoro, or the first phase of the plan when none is saved.</summary>
    public PomodoroState LoadPomodoro(PomodoroPlan plan) =>
        Read("SELECT duration_s, ends_at, remaining_s, pomodoro_phase, pomodoro_round FROM timers WHERE kind = 'pomodoro' LIMIT 1", reader =>
        {
            if (!reader.Read()) return PomodoroState.Initial(plan);
            var phase = Enum.TryParse<PomodoroPhase>(reader.GetString(3), out var parsed) ? parsed : PomodoroPhase.Focus;
            return new PomodoroState(phase, reader.GetInt32(4), ReadTimer(reader, 0));
        });

    public void SavePomodoro(PomodoroState pomodoro) => Upsert("pomodoro", pomodoro.Clock, pomodoro.Phase.ToString(), pomodoro.Round);

    public StopwatchState LoadStopwatch() =>
        Read("SELECT started_at, accumulated_ms, laps FROM stopwatch LIMIT 1",
            reader => reader.Read() ? new StopwatchState(Time(reader, 0), reader.GetInt64(1), ReadLaps(reader.GetString(2))) : StopwatchState.Default);

    public void SaveStopwatch(StopwatchState stopwatch)
    {
        using var transaction = db.Connection.BeginTransaction();
        Execute(transaction, "DELETE FROM stopwatch");
        Execute(transaction, "INSERT INTO stopwatch (started_at, accumulated_ms, laps) VALUES ($started, $ms, $laps)",
            ("$started", Text(stopwatch.StartedAt)), ("$ms", stopwatch.AccumulatedMs), ("$laps", JsonSerializer.Serialize(stopwatch.Laps)));
        transaction.Commit();
    }

    private void Upsert(string kind, CountdownTimer timer, string? phase, int? round)
    {
        using var transaction = db.Connection.BeginTransaction();
        Execute(transaction, "DELETE FROM timers WHERE kind = $kind AND label = $label", ("$kind", kind), ("$label", Island));
        Execute(transaction,
            "INSERT INTO timers (kind, label, duration_s, ends_at, remaining_s, pomodoro_phase, pomodoro_round) " +
            "VALUES ($kind, $label, $duration, $ends, $remaining, $phase, $round)",
            ("$kind", kind), ("$label", Island), ("$duration", timer.TotalSeconds), ("$ends", Text(timer.EndsAt)),
            ("$remaining", timer.PausedRemaining is { } left ? (int)Math.Round(left.TotalSeconds) : DBNull.Value),
            ("$phase", (object?)phase ?? DBNull.Value), ("$round", (object?)round ?? DBNull.Value));
        transaction.Commit();
    }

    private static void Bind(SqliteCommand command, string label, CountdownTimer clock)
    {
        command.Parameters.AddWithValue("$label", label);
        command.Parameters.AddWithValue("$duration", clock.TotalSeconds);
        command.Parameters.AddWithValue("$ends", Text(clock.EndsAt));
        command.Parameters.AddWithValue("$remaining", clock.PausedRemaining is { } left ? (int)Math.Round(left.TotalSeconds) : DBNull.Value);
    }

    /// <summary>Laps are a JSON array of total times in ms; anything unreadable counts as no laps.</summary>
    private static long[] ReadLaps(string json)
    {
        try { return JsonSerializer.Deserialize<long[]>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    private static CountdownTimer ReadTimer(SqliteDataReader reader, int first) => new(
        reader.GetInt32(first),
        Time(reader, first + 1),
        reader.IsDBNull(first + 2) ? null : TimeSpan.FromSeconds(reader.GetInt32(first + 2)));

    private static DateTimeOffset? Time(SqliteDataReader reader, int column) =>
        reader.IsDBNull(column) ? null : DateTimeOffset.Parse(reader.GetString(column), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static object Text(DateTimeOffset? time) =>
        time is { } t ? t.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) : DBNull.Value;

    private T Read<T>(string sql, Func<SqliteDataReader, T> read)
    {
        using var command = db.Connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        return read(reader);
    }

    private void Execute(SqliteTransaction transaction, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = db.Connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        command.ExecuteNonQuery();
    }
}
