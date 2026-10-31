using Gili.Core.Timers;

namespace Gili.Core.Tests.Timers;

public class TimerModelsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 5, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Timer_counts_down_from_its_end()
    {
        var timer = CountdownTimer.Default.Start(Now);
        Assert.True(timer.IsRunning);
        Assert.Equal(TimeSpan.FromMinutes(7), timer.Remaining(Now.AddMinutes(3)));
        Assert.False(timer.IsDone(Now.AddMinutes(9)));
        Assert.True(timer.IsDone(Now.AddMinutes(10)));
    }

    [Fact]
    public void Paused_timer_keeps_its_remaining()
    {
        var paused = CountdownTimer.Default.Start(Now).Pause(Now.AddMinutes(4));
        Assert.True(paused.IsPaused);
        Assert.Equal(TimeSpan.FromMinutes(6), paused.Remaining(Now.AddHours(5)));
    }

    [Fact]
    public void Resume_sets_a_new_end()
    {
        var resumed = CountdownTimer.Default.Start(Now).Pause(Now.AddMinutes(4)).Start(Now.AddMinutes(20));
        Assert.Equal(Now.AddMinutes(26), resumed.EndsAt);
    }

    [Fact]
    public void Reset_goes_back_to_the_total()
    {
        var reset = CountdownTimer.Default.WithTotal(300).Start(Now).Reset();
        Assert.False(reset.IsRunning || reset.IsPaused);
        Assert.Equal(TimeSpan.FromSeconds(300), reset.Remaining(Now));
    }

    [Fact]
    public void Remaining_ignores_a_time_zone_change()
    {
        var timer = CountdownTimer.Default.Start(Now);
        var sameMomentElsewhere = Now.AddMinutes(3).ToOffset(TimeSpan.FromHours(9));
        Assert.Equal(TimeSpan.FromMinutes(7), timer.Remaining(sameMomentElsewhere));
    }

    [Fact]
    public void Stopwatch_adds_up_runs()
    {
        var sw = StopwatchState.Default.Start(Now).Pause(Now.AddSeconds(10)).Start(Now.AddSeconds(100));
        Assert.Equal(TimeSpan.FromSeconds(15), sw.Elapsed(Now.AddSeconds(105)));
        Assert.Equal(TimeSpan.Zero, sw.Reset().Elapsed(Now));
    }

    [Fact]
    public void Pomodoro_goes_focus_break_focus()
    {
        var focus = PomodoroState.Default.Start(Now);
        Assert.Equal((PomodoroPhase.Focus, 1, TimeSpan.FromMinutes(25)), (focus.Phase, focus.Round, focus.Clock.Remaining(Now)));
        var rest = focus.NextPhase(Now.AddMinutes(25), startNow: true, PomodoroPlan.Default);
        Assert.Equal((PomodoroPhase.Break, 1, TimeSpan.FromMinutes(5)), (rest.Phase, rest.Round, rest.Clock.Remaining(Now.AddMinutes(25))));
        var next = rest.NextPhase(Now.AddMinutes(30), startNow: false, PomodoroPlan.Default);
        Assert.Equal((PomodoroPhase.Focus, 2, false), (next.Phase, next.Round, next.Clock.IsRunning));
    }

    [Fact]
    public void Fourth_focus_leads_to_a_long_break()
    {
        var fourth = PomodoroState.Default with { Round = 4 };
        var rest = fourth.NextPhase(Now, startNow: true, PomodoroPlan.Default);
        Assert.Equal((PomodoroPhase.LongBreak, TimeSpan.FromMinutes(15)), (rest.Phase, rest.Clock.Remaining(Now)));
    }

    [Fact]
    public void Long_break_starts_over_at_session_1()
    {
        var longBreak = new PomodoroState(PomodoroPhase.LongBreak, 4, CountdownTimer.Default.WithTotal(900));
        var next = longBreak.NextPhase(Now, true, PomodoroPlan.Default);
        Assert.Equal((PomodoroPhase.Focus, 1), (next.Phase, next.Round));
    }

    [Fact]
    public void Skip_starts_the_next_focus()
    {
        var skipped = PomodoroState.Default.Skip(Now, PomodoroPlan.Default);
        Assert.Equal((PomodoroPhase.Focus, 2, true), (skipped.Phase, skipped.Round, skipped.Clock.IsRunning));
        Assert.Equal(1, (PomodoroState.Default with { Round = 4 }).Skip(Now, PomodoroPlan.Default).Round);
    }

    private static LabeledTimer Labeled(long id, string label, int seconds, int endsInSeconds) =>
        new(id, label, CountdownTimer.Default.WithTotal(seconds) with { EndsAt = Now.AddSeconds(endsInSeconds) });

    [Fact]
    public void Timers_ending_together_finish_in_end_order()
    {
        var timers = new[] { Labeled(3, "C", 60, 10), Labeled(1, "A", 60, 10), Labeled(2, "B", 30, 5), Labeled(4, "D", 60, 99) };
        Assert.Equal([2L, 1L, 3L], TimerSchedule.Finished(timers, Now.AddSeconds(10)));
        Assert.Empty(TimerSchedule.Finished(timers, Now));
    }

    [Fact]
    public void Soonest_is_the_earliest_end_and_ignores_paused()
    {
        var late = CountdownTimer.Default.Start(Now);
        var early = CountdownTimer.Default.WithTotal(60).Start(Now);
        var paused = CountdownTimer.Default.WithTotal(5).Start(Now).Pause(Now.AddSeconds(1));
        Assert.Equal(early, TimerSchedule.Soonest([late, paused, early], Now));
        Assert.Null(TimerSchedule.Soonest([paused, CountdownTimer.Default], Now));
        Assert.Null(TimerSchedule.Soonest([], Now));
    }

    [Fact]
    public void Slot_shows_the_island_timer_when_it_ends_with_the_labeled_one()
    {
        var island = CountdownTimer.Default.WithTotal(60).Start(Now);
        var labeled = CountdownTimer.Default.WithTotal(60).Start(Now);
        Assert.False(TimerSchedule.SlotShowsLabeled(island, labeled, pomodoroRunning: false));
    }

    [Fact]
    public void Slot_shows_a_labeled_timer_that_ends_first()
    {
        var island = CountdownTimer.Default.WithTotal(120).Start(Now);
        var labeled = CountdownTimer.Default.WithTotal(60).Start(Now);
        Assert.True(TimerSchedule.SlotShowsLabeled(island, labeled, pomodoroRunning: false));
        Assert.False(TimerSchedule.SlotShowsLabeled(labeled, island, pomodoroRunning: false));
    }

    [Fact]
    public void Slot_shows_a_labeled_timer_when_the_island_timer_is_not_running()
    {
        var labeled = CountdownTimer.Default.WithTotal(600).Start(Now);
        var paused = CountdownTimer.Default.WithTotal(5).Start(Now).Pause(Now.AddSeconds(1));
        Assert.True(TimerSchedule.SlotShowsLabeled(CountdownTimer.Default, labeled, pomodoroRunning: false));
        Assert.True(TimerSchedule.SlotShowsLabeled(paused, labeled, pomodoroRunning: false));
        Assert.False(TimerSchedule.SlotShowsLabeled(CountdownTimer.Default, null, pomodoroRunning: false));
    }

    [Fact]
    public void Slot_shows_a_running_pomodoro_over_any_labeled_timer()
    {
        var labeled = CountdownTimer.Default.WithTotal(60).Start(Now);
        Assert.False(TimerSchedule.SlotShowsLabeled(CountdownTimer.Default, labeled, pomodoroRunning: true));
    }

    [Fact]
    public void Empty_label_reads_timer_and_long_labels_are_cut()
    {
        Assert.Equal("Timer", LabeledTimer.CleanLabel("   "));
        Assert.Equal("Tea", LabeledTimer.CleanLabel("  Tea "));
        Assert.Equal(new string('a', 40), LabeledTimer.CleanLabel(new string('a', 60)));
        Assert.Equal((10, 40), (LabeledTimer.MaxCount, LabeledTimer.LabelLimit));
    }

    [Fact]
    public void Lap_adds_the_total_and_only_while_running()
    {
        Assert.False(StopwatchState.Default.CanLap);
        Assert.Empty(StopwatchState.Default.Lap(Now).Laps);
        var sw = StopwatchState.Default.Start(Now);
        Assert.True(sw.CanLap);
        var lapped = sw.Lap(Now.AddSeconds(12)).Lap(Now.AddSeconds(20));
        Assert.Equal([12_000L, 20_000L], lapped.Laps);
        var paused = lapped.Pause(Now.AddSeconds(30));
        Assert.Equal([12_000L, 20_000L], paused.Laps);
        Assert.Equal(30_000, paused.AccumulatedMs);
        Assert.Equal(lapped.Laps, paused.Start(Now.AddSeconds(40)).Laps);
    }

    [Fact]
    public void Laps_stop_at_99()
    {
        var sw = StopwatchState.Default.Start(Now);
        for (var i = 1; i <= 120; i++) sw = sw.Lap(Now.AddSeconds(i));
        Assert.Equal(99, sw.Laps.Count);
        Assert.False(sw.CanLap);
        Assert.Equal(99_000, sw.Laps[^1]);
    }

    [Fact]
    public void Reset_clears_laps()
    {
        var lapped = StopwatchState.Default.Start(Now).Lap(Now.AddSeconds(3));
        var reset = lapped.Reset();
        Assert.Empty(reset.Laps);
        Assert.Equal(StopwatchState.Default, reset);
        Assert.Equal(lapped, StopwatchState.Default.Start(Now).Lap(Now.AddSeconds(3)));
        Assert.NotEqual(lapped, StopwatchState.Default.Start(Now).Lap(Now.AddSeconds(4)));
    }

    [Fact]
    public void Plan_lengths_drive_each_phase()
    {
        var plan = new PomodoroPlan(20, 4, 12, 3);
        var focus = PomodoroState.Initial(plan);
        Assert.Equal(TimeSpan.FromMinutes(20), focus.Clock.Total);
        var rest = focus.NextPhase(Now, startNow: true, plan);
        Assert.Equal((PomodoroPhase.Break, TimeSpan.FromMinutes(4)), (rest.Phase, rest.Clock.Total));
        var third = new PomodoroState(PomodoroPhase.Focus, 3, focus.Clock).NextPhase(Now, startNow: false, plan);
        Assert.Equal((PomodoroPhase.LongBreak, TimeSpan.FromMinutes(12)), (third.Phase, third.Clock.Total));
        Assert.Equal(TimeSpan.FromMinutes(12), plan.Length(PomodoroPhase.LongBreak));
        Assert.Equal(1, third.NextPhase(Now, true, plan).Round);
        Assert.Equal(1, new PomodoroState(PomodoroPhase.Focus, 3, focus.Clock).Skip(Now, plan).Round);
        Assert.Equal(TimeSpan.FromMinutes(20), PomodoroState.Reset(plan).Clock.Total);
    }

    [Fact]
    public void A_plan_change_leaves_a_running_phase_alone()
    {
        var plan = new PomodoroPlan(50, 10, 30, 4);
        var running = PomodoroState.Default.Start(Now);
        Assert.Equal(running, running.WithPlan(plan));
        var paused = running.Pause(Now.AddMinutes(5));
        Assert.Equal(paused, paused.WithPlan(plan));
    }

    [Fact]
    public void A_plan_change_resizes_an_unstarted_phase()
    {
        var plan = new PomodoroPlan(50, 10, 30, 4);
        Assert.Equal(TimeSpan.FromMinutes(50), PomodoroState.Default.WithPlan(plan).Clock.Total);
        var rest = PomodoroState.Default.NextPhase(Now, startNow: false, PomodoroPlan.Default);
        Assert.Equal(TimeSpan.FromMinutes(5), rest.Clock.Total);
        Assert.Equal(TimeSpan.FromMinutes(10), rest.WithPlan(plan).Clock.Total);
    }

    [Fact]
    public void Lowering_rounds_below_the_session_ends_in_a_long_break()
    {
        var plan = PomodoroPlan.Default with { Rounds = 3 };
        var lowered = (PomodoroState.Default with { Round = 4 }).Start(Now).WithPlan(plan);
        Assert.Equal(3, lowered.Round);
        Assert.True(lowered.Clock.IsRunning);
        Assert.Equal(PomodoroPhase.LongBreak, lowered.NextPhase(Now.AddMinutes(25), startNow: true, plan).Phase);
    }

    [Fact]
    public void A_break_after_lowering_rounds_starts_session_one()
    {
        var plan = PomodoroPlan.Default with { Rounds = 3 };
        var rest = new PomodoroState(PomodoroPhase.Break, 3, CountdownTimer.Default.WithTotal(300));
        var running = rest.Start(Now).WithPlan(plan).NextPhase(Now.AddMinutes(5), startNow: true, plan);
        var unstarted = rest.WithPlan(plan).NextPhase(Now, startNow: false, plan);
        Assert.Equal((PomodoroPhase.Focus, 1), (running.Phase, running.Round));
        Assert.Equal((PomodoroPhase.Focus, 1), (unstarted.Phase, unstarted.Round));
        Assert.Equal("Session 1 of 3 ready", TimerText.BreakOver(rest, plan));
    }

    [Fact]
    public void Plan_is_clamped_to_its_ranges()
    {
        Assert.Equal(new PomodoroPlan(1, 1, 1, 2), new PomodoroPlan(0, -5, 0, 1).Normalize());
        Assert.Equal(new PomodoroPlan(120, 60, 60, 8), new PomodoroPlan(500, 90, 99, 20).Normalize());
        Assert.Equal(PomodoroPlan.Default, PomodoroPlan.Default.Normalize());
        Assert.Equal(new PomodoroPlan(25, 5, 15, 4), PomodoroPlan.Default);
    }
}
