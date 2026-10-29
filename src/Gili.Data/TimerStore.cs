using System.Globalization;
using Gili.Core.Timers;
using Microsoft.Data.Sqlite;

namespace Gili.Data;

/// <summary>
/// The island's timer, pomodoro and stopwatch in gili.db (PRD 10), one row each. Times are UTC; a paused timer keeps
/// whole seconds left.
/// </summary>
public sealed class TimerStore(Database db)
{
    private const string Island = "";

    public CountdownTimer LoadTimer() =>
        Read("SELECT duration_s, ends_at, remaining_s FROM timers WHERE kind = 'timer' AND label = '' LIMIT 1",
            reader => reader.Read() ? ReadTimer(reader, 0) : CountdownTimer.Default);

    public void SaveTimer(CountdownTimer timer) => Upsert("timer", timer, null, null);

    public PomodoroState LoadPomodoro() =>
        Read("SELECT duration_s, ends_at, remaining_s, pomodoro_phase, pomodoro_round FROM timers WHERE kind = 'pomodoro' LIMIT 1", reader =>
        {
            if (!reader.Read()) return PomodoroState.Default;
            var phase = Enum.TryParse<PomodoroPhase>(reader.GetString(3), out var parsed) ? parsed : PomodoroPhase.Focus;
            return new PomodoroState(phase, reader.GetInt32(4), ReadTimer(reader, 0));
        });

    public void SavePomodoro(PomodoroState pomodoro) => Upsert("pomodoro", pomodoro.Clock, pomodoro.Phase.ToString(), pomodoro.Round);

    public StopwatchState LoadStopwatch() =>
        Read("SELECT started_at, accumulated_ms FROM stopwatch LIMIT 1",
            reader => reader.Read() ? new StopwatchState(Time(reader, 0), reader.GetInt64(1), []) : StopwatchState.Default);

    public void SaveStopwatch(StopwatchState stopwatch)
    {
        using var transaction = db.Connection.BeginTransaction();
        Execute(transaction, "DELETE FROM stopwatch");
        Execute(transaction, "INSERT INTO stopwatch (started_at, accumulated_ms) VALUES ($started, $ms)",
            ("$started", Text(stopwatch.StartedAt)), ("$ms", stopwatch.AccumulatedMs));
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
