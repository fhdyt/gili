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
        var pomodoro = PomodoroState.Default.NextPhase(Now, startNow: true, PomodoroPlan.Default);
        Assert.Equal(pomodoro, AfterRestart(s => s.SavePomodoro(pomodoro), s => s.LoadPomodoro(PomodoroPlan.Default)));
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
        Assert.Equal((CountdownTimer.Default, PomodoroState.Default, StopwatchState.Default), (store.LoadTimer(), store.LoadPomodoro(PomodoroPlan.Default), store.LoadStopwatch()));
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

    [Fact]
    public void Labeled_timers_survive_a_restart()
    {
        var running = CountdownTimer.Default.WithTotal(600).Start(Now);
        var paused = CountdownTimer.Default.WithTotal(90).Start(Now).Pause(Now.AddSeconds(42));
        var saved = AfterRestart(s => { s.AddLabeled("Tea", running); s.AddLabeled("", paused); }, s => s.LoadLabeled());
        Assert.Equal(["Tea", "Timer"], saved.Select(t => t.Label));
        Assert.Equal([running, paused], saved.Select(t => t.Clock));
        Assert.True(saved[0].Id < saved[1].Id);
    }

    [Fact]
    public void Island_timer_and_labeled_timers_stay_apart()
    {
        using var db = Database.Open(_folder);
        var store = new TimerStore(db);
        var tea = store.AddLabeled("Tea", CountdownTimer.Default.WithTotal(600));
        store.SaveTimer(CountdownTimer.Default.WithTotal(30));
        Assert.Equal([tea], store.LoadLabeled());
        Assert.Equal(30, store.LoadTimer().TotalSeconds);
        store.UpdateLabeled(tea with { Clock = tea.Clock.Start(Now) });
        store.DeleteLabeled(tea.Id);
        Assert.Equal(30, store.LoadTimer().TotalSeconds);
        store.AddLabeled("Egg", CountdownTimer.Default.WithTotal(300));
        store.SaveTimer(CountdownTimer.Default.WithTotal(45));
        Assert.Single(store.LoadLabeled());
    }

    [Fact]
    public void Update_and_delete_a_labeled_timer()
    {
        using var db = Database.Open(_folder);
        var store = new TimerStore(db);
        var tea = store.AddLabeled("Tea", CountdownTimer.Default.WithTotal(600));
        var egg = store.AddLabeled("Egg", CountdownTimer.Default.WithTotal(300));
        var changed = tea with { Label = "Green tea", Clock = tea.Clock.Start(Now) };
        store.UpdateLabeled(changed);
        Assert.Equal([changed, egg], store.LoadLabeled());
        store.DeleteLabeled(tea.Id);
        Assert.Equal([egg], store.LoadLabeled());
    }

    [Fact]
    public void Laps_survive_a_restart()
    {
        var stopwatch = StopwatchState.Default.Start(Now) with { Laps = [1500, 61_250, 6_000_000] };
        Assert.Equal(stopwatch, AfterRestart(s => s.SaveStopwatch(stopwatch), s => s.LoadStopwatch()));
    }

    [Fact]
    public void Broken_laps_read_as_none()
    {
        using var db = Database.Open(_folder);
        var store = new TimerStore(db);
        store.SaveStopwatch(StopwatchState.Default.Start(Now));
        using var command = db.Connection.CreateCommand();
        command.CommandText = "UPDATE stopwatch SET laps = '{ not json'";
        command.ExecuteNonQuery();
        Assert.Empty(store.LoadStopwatch().Laps);
    }

    [Fact]
    public void Pomodoro_starts_from_the_given_plan_when_nothing_is_saved()
    {
        using var db = Database.Open(_folder);
        var plan = new PomodoroPlan(50, 10, 30, 3);
        Assert.Equal(PomodoroState.Initial(plan), new TimerStore(db).LoadPomodoro(plan));
    }
}
