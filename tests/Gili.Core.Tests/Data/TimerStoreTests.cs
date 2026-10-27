using Gili.Core.Timers;
using Gili.Data;
using Microsoft.Data.Sqlite;

namespace Gili.Core.Tests.Data;

public sealed class TimerStoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 5, 0, 0, TimeSpan.Zero);
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "gili-timers-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    /// <summary>Saves with one database, then reads with a fresh one from the same file, like a restart.</summary>
    private T AfterRestart<T>(Action<TimerStore> save, Func<TimerStore, T> load)
    {
        using (var db = Database.Open(_folder)) save(new TimerStore(db));
        SqliteConnection.ClearAllPools();
        using var reopened = Database.Open(_folder);
        return load(new TimerStore(reopened));
    }

    [Fact]
    public void Running_timer_survives_a_restart()
    {
        var timer = CountdownTimer.Default.WithTotal(300).Start(Now);
        Assert.Equal(timer, AfterRestart(s => s.SaveTimer(timer), s => s.LoadTimer()));
    }

    [Fact]
    public void Paused_timer_survives_a_restart()
    {
        var timer = CountdownTimer.Default.Start(Now).Pause(Now.AddSeconds(42));
        Assert.Equal(timer, AfterRestart(s => s.SaveTimer(timer), s => s.LoadTimer()));
    }

    [Fact]
    public void Pomodoro_phase_and_round_survive()
    {
        var pomodoro = PomodoroState.Default.NextPhase(Now, startNow: true);
        Assert.Equal(pomodoro, AfterRestart(s => s.SavePomodoro(pomodoro), s => s.LoadPomodoro()));
    }

    [Fact]
    public void Stopwatch_survives()
    {
        var stopwatch = StopwatchState.Default.Start(Now).Pause(Now.AddSeconds(12)).Start(Now.AddMinutes(1));
        Assert.Equal(stopwatch, AfterRestart(s => s.SaveStopwatch(stopwatch), s => s.LoadStopwatch()));
    }

    [Fact]
    public void Nothing_saved_gives_defaults()
    {
        using var db = Database.Open(_folder);
        var store = new TimerStore(db);
        Assert.Equal((CountdownTimer.Default, PomodoroState.Default, StopwatchState.Default), (store.LoadTimer(), store.LoadPomodoro(), store.LoadStopwatch()));
    }

    [Fact]
    public void Saving_twice_keeps_one_row()
    {
        using var db = Database.Open(_folder);
        var store = new TimerStore(db);
        store.SaveTimer(CountdownTimer.Default.Start(Now));
        store.SaveTimer(CountdownTimer.Default.WithTotal(10));
        store.SaveStopwatch(StopwatchState.Default.Start(Now));
        store.SaveStopwatch(StopwatchState.Default);
        using var count = db.Connection.CreateCommand();
        count.CommandText = "SELECT (SELECT COUNT(*) FROM timers WHERE kind = 'timer') + (SELECT COUNT(*) FROM stopwatch)";
        Assert.Equal(2L, count.ExecuteScalar());
        Assert.Equal(10, store.LoadTimer().TotalSeconds);
    }
}
